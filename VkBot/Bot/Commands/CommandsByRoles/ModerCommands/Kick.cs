﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VkBot.Data.Abstractions;
using VkBot.Domain;
using VkBot.Domain.Models;
using VkNet.Abstractions;
using VkNet.Model;
using User = VkNet.Model.User;

namespace VkBot.Bot.Commands.CommandsByRoles.ModerCommands
{
    public class Kick : IBotCommand
    {
        private readonly MainContext _db;
        private readonly IVkApi _vkApi;
        private readonly RolesHandler _checker;
        public string[] Aliases { get; set; } = { "кик", "выгнать" };

        public string Description { get; set; } =
            "Команда !Бот кик кикает того пользоователя, чьё сообщение в чате вы переслали, или тому пользователю, к которому вы обратились по тегу.\nПример: !Бот кик + пересланное сообщение\n" +
            "ВАЖНО: КОМАНДА РАБОТАЕТ ТОЛЬКО С ПРАВАМИ МОДЕРАТОРА И ВЫШЕ!";

        public Kick(MainContext db, IVkApi api, RolesHandler checker)
        {
            _db = db;
            _vkApi = api;
            _checker = checker;
        }

        public async Task<string> Execute(Message msg)
        {
            if(msg.PeerId.Value == msg.FromId.Value)
            {
                return "Команда работает только в групповых чатах!";
            }

            if(!await _checker.CheckAccessToCommand(msg.FromId.Value, msg.PeerId.Value, Roles.Moderator))
            {
                return "Недосточно прав!";
            }

           

            var forwardMessage = msg.ForwardedMessages.Count == 0 ? msg.ReplyMessage : msg.ForwardedMessages[0];

            User kickedUser;
            if(forwardMessage is null)
            {
                var split = msg.Text.Split(' ', 2); // [команда, параметры]

                if(split.Length < 2)
                {
                    return "Указаны не все параметры!";
                }

                var userID = long.Parse(msg.Text.Substring(msg.Text.IndexOf("[") + 3,
                                                    msg.Text.IndexOf('|') - msg.Text.IndexOf('[') - 3));

                kickedUser = _vkApi.Messages.GetConversationMembers(msg.PeerId.Value, new[] { "" })
                                 .Profiles.FirstOrDefault(x => x.Id == userID);

            }
            else
            {
                kickedUser = _vkApi.Messages.GetConversationMembers(msg.PeerId.Value, new[] { "" })
                                 .Profiles.FirstOrDefault(x => x.Id == forwardMessage.FromId.Value);
            }


            if(kickedUser is null)
            {
                return "Данного пользователя нет в этом чате!";
            }

            if((await _db.Users.FirstOrDefaultAsync(x => x.Vk == kickedUser.Id)).IsBotAdmin)
            {
                return "Вы не можете кикнуть этого пользователю, так как он администратор бота!";
            }

            if(await _checker.GetUserRole(kickedUser.Id, msg.PeerId.Value) >=
               await _checker.GetUserRole(msg.FromId.Value, msg.PeerId.Value))
            {
                if(!(await _db.Users.FirstOrDefaultAsync(x => x.Vk == msg.FromId.Value)).IsBotAdmin)
                {
                    return "Вы не можете кикнуть этого пользователю т.к у него больше или такие же права!";
                }
            }

            try
            {
                _vkApi.Messages.RemoveChatUser((ulong) msg.PeerId.Value - 2000000000, kickedUser.Id);
            }
            catch(Exception)
            {
                return "Упс...Что-то пошло не так, возможно у меня недостаточно прав!";
            }

            _db.ChatRoles.Remove(await _db.ChatRoles.FirstOrDefaultAsync(x => x.UserVkID == kickedUser.Id &&
                                                                              x.ChatVkID == msg.PeerId.Value));
            await _db.SaveChangesAsync();

            return "Пользователь исключён!";
        }
    }
}