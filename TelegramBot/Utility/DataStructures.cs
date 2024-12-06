using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Text.Json.Serialization;

namespace TelegramBot.Utility;

// Chat and Message data structures

internal class TelegramChatArg(ETelegramChatArg type, CallbackQuery callbackQuery, Message interactionMessage)
{
	public readonly CallbackQuery CallbackQuery = callbackQuery;
	public readonly Message InteractionMessage = interactionMessage;
	public readonly ETelegramChatArg Type = type;
}
internal class TelegramChatArg<T>(ETelegramChatArg type, CallbackQuery callbackQuery, Message interactionMessage, T arg) 
	: TelegramChatArg(type, callbackQuery, interactionMessage)
{
	public readonly T Arg = arg;
}
internal struct MessageStats
{
	public Message Message;
	public string FullMessage;
	public string Command;
	public string Text;
	public List<string> TextList;
	public long ChatId;
	public long UserId;
	public int MessageId;
	public ChatType ChatType;
}

// Config Data Structure

[method: Newtonsoft.Json.JsonConstructor]
internal readonly struct DataStruct(
	Dictionary<long, string> usernames,
	Dictionary<long, string> groups,
	List<long> rootPermissions,
	List<long> debtChats,
	List<long> debtUsers)
{
	[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
	public Dictionary<long, string> Usernames { get; } = usernames;
	[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
	public Dictionary<long, string> Groups { get; } = groups;
	[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
	public List<long> RootPermissions { get; } = rootPermissions;
	[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
	public List<long> DebtChats { get; } = debtChats;
	[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
	public List<long> DebtUsers { get; } = debtUsers;
}

// Hardware data structure

internal static class HardwareEmoji
{
	public const string Bulb = "💡";
	public const string Pc = "🖥";
	public const string Thunder = "⚡";
	public const string Reboot = "🔄";
	public const string On = "\ud83c\udf15\ud83c\udf15\ud83c\udf15";
	public const string Off = "\ud83c\udf11\ud83c\udf11\ud83c\udf11";
}

// Debts data structures

[method: JsonConstructor]
internal class Debts(
	double amount,
	long towards)
{
	public double Amount { get; set; } = amount;
	public long Towards { get; } = towards;
}

internal class CurrentPurchase
{
	public readonly Stack<SubPurchase> History = new();
	public readonly List<int> MessagesToDelete = [];
	public readonly Dictionary<long, double> Purchases = new();
	public long Buyer;
}

internal class SubPurchase
{
	public List<long> Customers = [];
	public double TotalAmount;
}

