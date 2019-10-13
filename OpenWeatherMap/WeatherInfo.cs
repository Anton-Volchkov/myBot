﻿using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Flurl.Http;
using Newtonsoft.Json;
using OpenWeatherMap.Models.Daily;

namespace OpenWeatherMap
{
    public class WeatherInfo
    {
        private const string EndPoint = "https://api.openweathermap.org/data/2.5/";
        private const string Lang = "ru";

        private const double PressureConvert = 0.75006375541921;
        private readonly string Token;

        public WeatherInfo(string token)
        {
            if(string.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException(nameof(token), "Токен отсутствует");
            }

            Token = token;
        }

        public async Task<string> GetWeather(string city)
        {
            city = char.ToUpper(city[0]) + city.Substring(1); //TODO ?

            var response = await GetCurrentWeatherResponse(city);

            if(!response.IsSuccessStatusCode)
            {
                var newCity = city.Replace("е", "ё");

                response = await GetCurrentWeatherResponse(newCity);

                if(!response.IsSuccessStatusCode)
                {
                    return $"Город {city} не найден.";
                }

                city = newCity;
            }

            var w = JsonConvert.DeserializeObject<Models.WeatherInfo>(await response.Content.ReadAsStringAsync());

            var strBuilder = new StringBuilder();
            strBuilder.AppendFormat("Погода {0} на данный момент", city).AppendLine();
            strBuilder.AppendLine("_____________").AppendLine();
            strBuilder.AppendFormat("Средняя температура: {0:N0}°С", w.Weather.Temperature).AppendLine();
            strBuilder.AppendFormat("Описание погоды: {0}", w.Info[0].State).AppendLine();
            strBuilder.AppendFormat("Скорость ветра: {0:N0} м/с", w.Wind.Speed).AppendLine();
            strBuilder.AppendFormat("Направление: {0}", w.Wind.Degrees).AppendLine();
            strBuilder.AppendFormat("Влажность: {0}%", w.Weather.Humidity).AppendLine();
            strBuilder.AppendFormat("Давление: {0:N0} мм.рт.ст", w.Weather.Pressure * PressureConvert).AppendLine();
            strBuilder.AppendFormat("Рассвет в {0:HH:mm}", UnixToDateTime(w.OtherInfo.Sunrise)).AppendLine();
            strBuilder.AppendFormat("Закат в {0:HH:mm}", UnixToDateTime(w.OtherInfo.Sunset)).AppendLine();
            strBuilder.AppendLine("_____________");

            return strBuilder.ToString();
        }

        public async Task<string> GetDailyWeather(string city)
        {
            city = char.ToUpper(city[0]) + city.Substring(1); //TODO ?
            var date = DateTime.Now;
            const int count = 8;

            var response = await GetDailyWeatherResponse(city, count);

            if(!response.IsSuccessStatusCode)
            {
                var newCity = city.Replace("е", "ё");
                response = await GetDailyWeatherResponse(city, count);

                if(!response.IsSuccessStatusCode)
                {
                    return $"Город {city} не найден. Проверьте введённые данные.";
                }

                city = newCity;
            }

            var weatherToday = JsonConvert.DeserializeObject<DailyWeather>(await response.Content.ReadAsStringAsync());

            if(weatherToday is null)
            {
                return $"Погода на {date:dd.MM} не найдена";
            }

            var strBuilder = new StringBuilder();
            strBuilder.AppendFormat("Погода в городе {0} на сегодня ({1:dddd, d MMMM}):", city, date).AppendLine()
                      .AppendLine();
            foreach(var weatherHourly in weatherToday.List)
            {
                strBuilder.AppendFormat("Время: {0:HH:mm (dd.MM.yyyy)}", weatherHourly.DtTxt).AppendLine();
                strBuilder.AppendFormat("Температура: {0:+#;-#;0}°С", weatherHourly.Main.Temp).AppendLine();
                strBuilder.AppendFormat("Описание погоды: {0}", weatherHourly.Weather[0].Description).AppendLine();
                strBuilder.AppendFormat("Влажность: {0}%", weatherHourly.Main.Humidity).AppendLine();
                strBuilder.AppendFormat("Ветер: {0:N0} м/с", weatherHourly.Wind.Speed).AppendLine();
                strBuilder.AppendFormat("Давление: {0:N0} мм.рт.ст", weatherHourly.Main.Pressure * PressureConvert)
                          .AppendLine();
                strBuilder.AppendFormat("Облачность: {0}%", weatherHourly.Clouds.All).AppendLine().AppendLine();
            }

            return strBuilder.ToString();
        }

        private async Task<HttpResponseMessage> GetCurrentWeatherResponse(string city)
        {
            return await BuildRequest()
                         .AppendPathSegment("weather")
                         .SetQueryParam("q", city)
                         .GetAsync();
        }

        private async Task<HttpResponseMessage> GetDailyWeatherResponse(string city, int count)
        {
            return await BuildRequest()
                         .AppendPathSegment("forecast")
                         .SetQueryParam("q", city)
                         .SetQueryParam("cnt", count)
                         .GetAsync();
        }

        private IFlurlRequest BuildRequest()
        {
            return EndPoint.AllowAnyHttpStatus()
                           .SetQueryParam("units", "metric")
                           .SetQueryParam("appid", Token)
                           .SetQueryParam("lang", Lang);
        }

        internal DateTime UnixToDateTime(double unixTimeStamp)
        {
            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }
    }
}