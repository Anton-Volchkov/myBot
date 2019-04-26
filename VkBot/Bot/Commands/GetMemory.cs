﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VkBot.Data.Abstractions;
using VkBot.Data.Models;
using VkNet.Abstractions;
using VkNet.Model;

namespace VkBot.Bot.Commands
{
    public class GetMemory : IBotCommand
    {
        public string[] Alliases { get; set; } = {"память"};
        private readonly MainContext _db;
        private readonly IVkApi _vkApi;

        public GetMemory(MainContext db, IVkApi api)
        {
            _vkApi = api;
            _db = db;    
        }

        public async Task<string> Execute(Message msg)
        {
            var user = (await _vkApi.Users.GetAsync(new[] { msg.FromId.Value })).FirstOrDefault();

            var userMemory = await _db.Memories.FirstOrDefaultAsync(x => x.UserID == msg.FromId.Value);

            if (userMemory == null)
            {
                return $"{user.FirstName} {user.LastName} - Я вас еще не знаю. ";
            }
            string sendText = string.IsNullOrWhiteSpace(userMemory.Memory)? "Ваших данных нет в базе!" : userMemory.Memory;
            return $"{user.FirstName} {user.LastName} ваши данные: \n {sendText}";
        }
    }
}