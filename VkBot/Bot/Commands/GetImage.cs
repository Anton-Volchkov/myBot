using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using ImageFinder;
using ImageFinder.Models;
using Microsoft.Extensions.Logging;
using VkBot.Controllers;
using VkBot.Data.Abstractions;
using VkNet.Abstractions;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;

namespace VkBot.Bot.Commands
{
    public class GetImage : IBotCommand
    {
        public string[] Aliases { get; set; } = { "картинки", "картинка" };

        public string Description { get; set; } =
            "Команда !Бот картинки + *текст* покажет вам найденные картнки по вашему запросу" +
            "\nПример: !Бот картнки + кот";

        private readonly IVkApi _vkApi;
        private readonly ImageProvider _provider;
        private readonly ILogger<GetImage> _logger;

        public GetImage(IVkApi vkApi, ImageProvider provider, ILogger<GetImage> logger)
        {
            _vkApi = vkApi;
            _provider = provider;
            _logger = logger;
        }

        public async Task<string> Execute(Message msg)
        {
            var split = msg.Text.Split(' ', 2); // [команда, параметры]

            if (split.Length < 2)
            {
                return "Не все параметры указаны!";
            }

            var url = await _provider.GetImagesUrl(split[1],Browser.Yandex);

            if(url.Count == 0)
            {
                _logger.LogCritical($"Неудалось получить картинки через Yandex, пробуем получить через DuckDuckGo");
                url = await _provider.GetImagesUrl(split[1], Browser.DuckDuckGo);

                if (url.Count == 0)
                {
                    _logger.LogCritical($"Неудалось получить картинки через DuckDuckGo");
                }
            }
            else
            {
                _logger.LogCritical($"URL картинок получены, одна из них: {url[0]}");
            }
        

            // Получить адрес сервера для загрузки картинок в сообщении
            try
            {
                var uploadServer = _vkApi.Photo.GetMessagesUploadServer(0);

                _logger.LogCritical("Адресс для загрузки получен");

                // Загрузить картинки на сервер VK.
                var imagePath = new List<string>();

                foreach(var item in url)
                    imagePath.Add(await UploadFile(uploadServer.UploadUrl,
                                                   item, "jpg"));

                var attachment = new List<IReadOnlyCollection<Photo>>();

                if(imagePath.Count > 0)
                {
                    // Сохранить загруженный файл
                    foreach(var path in imagePath) attachment.Add(_vkApi.Photo.SaveMessagesPhoto(path));

               
                    _vkApi.Messages.Send(new MessagesSendParams
                    {
                        PeerId = msg.PeerId.Value, 
                        Message = "", 
                        Attachments = attachment.SelectMany(x => x), //Вложение
                        RandomId = new DateTime().Millisecond + Guid.NewGuid().ToByteArray().Sum(x => x)
                    });
                    return $"Картинки по запросу: \"{split[1]}\"";
                }

                return $"Картинок по запросу \"{split[1]}\" не найдено";
            }
            catch(Exception e)
            {
                _logger.LogCritical($"Произошла ошибка: {e.Message}");
                return $"Упс... При поиске картинок по запросу: \"{split[1]}\" что-то пошло не так...";
            }
        }

        private async Task<string> UploadFile(string serverUrl, string file, string fileExtension)
        {
            // Получение массива байтов из файла
            var data = GetBytes(file);

            // Создание запроса на загрузку файла на сервер
            using(var client = new HttpClient())
            {
                var requestContent = new MultipartFormDataContent();
                var content = new ByteArrayContent(data);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
                requestContent.Add(content, "file", $"file.{fileExtension}");

                var response = client.PostAsync(serverUrl, requestContent).Result;
                return Encoding.Default.GetString(await response.Content.ReadAsByteArrayAsync());
            }
        }

        private byte[] GetBytes(string fileUrl)
        {
            using(var webClient = new WebClient())
            {
                return webClient.DownloadData(fileUrl);
            }
        }
    }
}