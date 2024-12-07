﻿using System.Text.RegularExpressions;
using Kernel.Software;
using Kernel.Software.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Utility;

internal static class TelegramUtils
{
	public static readonly string StoragePath;
	
	private static TelegramBotClient _cortana = null!;
	public static readonly Dictionary<long, TelegramChatArg> ChatArgs;
	public static readonly DataStruct Data;

	static TelegramUtils()
	{
		StoragePath = Path.Combine(FileHandler.ProjectStoragePath, "Config/Telegram/");
		Data = FileHandler.LoadFile<DataStruct>(Path.Combine(StoragePath, "TelegramData.json"));
		ChatArgs = new Dictionary<long, TelegramChatArg>();
	}

	public static void Init(TelegramBotClient newClient)
	{
		_cortana = newClient;
	}

	public static void SendToUser(long userId, string message, bool notify = true)
	{
		var chat = new ChatId(userId);
		_cortana.SendMessage(chat, message, disableNotification: !notify);
	}

	public static string IdToName(long id)
	{
		if (Data.Usernames.TryGetValue(id, out string? name))
			return name;
		throw new CortanaException("User not found");
	}

	public static string IdToGroupName(long id)
	{
		if (Data.Groups.TryGetValue(id, out string? name))
			return name;
		throw new CortanaException("Group not found");
	}

	public static long NameToId(string name)
	{
		foreach ((long groupId, _) in Data.Usernames.Where(item => item.Value == name))
			return groupId;
		throw new CortanaException("User not found");
	}

	public static long NameToGroupId(string name)
	{
		foreach ((long groupId, _) in Data.Groups.Where(item => item.Value == name))
			return groupId;
		throw new CortanaException("Group not found");
	}

	public static bool CheckHardwarePermission(long userId)
	{
		return Data.RootPermissions.Contains(userId);
	}

	public static bool TryAddChatArg(long chatId, TelegramChatArg arg, CallbackQuery callbackQuery)
	{
		if (ChatArgs.TryAdd(chatId, arg)) return true;
		_cortana.AnswerCallbackQuery(callbackQuery.Id, "You already have an interaction going on! Finish it before continuing", true);
		return false;
	}

	public static (int, int, int, int) ParseTime(string text)
	{
		(int s, int m, int h, int d) times = (0, 0, 0, 0);
		var timeRegex = new Regex("^([0-9]+)([s,m,h,d])$");
		foreach (string time in text.Split())
		{
			Match match = timeRegex.Match(time);
			if (match.Success)
			{
				int value = int.Parse(match.Groups[1].Value);
				switch (match.Groups[2].Value)
				{
					case "s":
						times.s = value;
						break;
					case "m":
						times.m = value;
						break;
					case "h":
						times.h = value;
						break;
					case "d":
						times.h = value*24;
						break;
				}
			}
			else throw new CortanaException("Invalid time");
		}
		return times;
	}

	public static async void AnswerOrMessage(ITelegramBotClient cortana, string text, long chatId, CallbackQuery? callbackQuery, bool showAlert = true)
	{
		try
		{
			await cortana.AnswerCallbackQuery(callbackQuery!.Id, text, showAlert);
		}
		catch
		{
			await cortana.SendMessage(chatId, text);
		}
	}
}