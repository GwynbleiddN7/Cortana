using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Utility;

namespace TelegramBot.Modules
{
    static class HardwareEmoji
    {
        public const string BULB = "💡";
        public const string PC = "🖥";
        public const string THUNDER = "⚡";
        public const string REBOOT = "🔄";
        public const string ON = "🟩🟩🟩";
        public const string OFF = "🟥🟥🟥";
    }
    
    public static class HardwareModule
    {
        private static Dictionary<long, string> HardwareAction = new();
        
        public static async void ExecCommand(MessageStats messageStats, ITelegramBotClient cortana)
        {
            switch (messageStats.Command)
            {
                case "ip":
                    var ip = await Utility.HardwareDriver.GetPublicIP();
                    await cortana.SendMessage(messageStats.ChatID, $"IP: {ip}");
                    break;
                case "gateway":
                    var gateway = Utility.HardwareDriver.GetDefaultGateway();
                    await cortana.SendMessage(messageStats.ChatID, $"Gateway: {gateway}");
                    break;
                case "temperature":
                    var temp = Utility.HardwareDriver.GetCPUTemperature();
                    await cortana.SendMessage(messageStats.ChatID, $"Temperature: {temp}");
                    break;
                case "hardware":
                    if (TelegramData.CheckPermission(messageStats.UserID))
                        await cortana.SendMessage(messageStats.ChatID, "Hardware Keyboard", replyMarkup: CreateHardwareButtons());
                    else
                        await cortana.SendMessage(messageStats.ChatID, "Not enough privileges");
                    break;
                case "keyboard":
                    if (TelegramData.CheckPermission(messageStats.UserID))
                        await cortana.SendMessage(messageStats.ChatID, "Hardware Toggle Keyboard", replyMarkup: CreateHardwareToggles());
                    else
                        await cortana.SendMessage(messageStats.ChatID, "Not enough privileges");
                    break;
                case "reboot":
                    if (TelegramData.CheckPermission(messageStats.UserID))
                    {
                        var res = Utility.HardwareDriver.PowerRaspberry(EPowerOption.Reboot);
                        await cortana.SendMessage(messageStats.ChatID, res);
                    }
                    else await cortana.SendMessage(messageStats.ChatID, "Not enough privileges");
                    break;
                case "shutdown":
                    if (TelegramData.CheckPermission(messageStats.UserID))
                    {
                        var res = Utility.HardwareDriver.PowerRaspberry(EPowerOption.Shutdown);
                        await cortana.SendMessage(messageStats.ChatID, res);
                    }
                    else await cortana.SendMessage(messageStats.ChatID, "Not enough privileges");
                    break;
                case "notify":
                    if (TelegramData.CheckPermission(messageStats.UserID))
                    {
                        var res = Utility.HardwareDriver.NotifyPC(messageStats.Text);
                        if (res == "0") await cortana.DeleteMessage(messageStats.ChatID, messageStats.MessageID);
                        else await cortana.SendMessage(messageStats.ChatID, res);
                    }
                    else await cortana.SendMessage(messageStats.ChatID, "Not enough privileges");
                    break;
            }
        }
        
        public static async void HandleCallback(MessageStats messageStats, ITelegramBotClient cortana)
        {
            if (!TelegramData.CheckPermission(messageStats.UserID) || messageStats.ChatType != ChatType.Private) return;
            switch (messageStats.FullMessage)
            {
                case HardwareEmoji.BULB:
                    Utility.HardwareDriver.SwitchLamp(EHardwareTrigger.Toggle);
                    break;
                case HardwareEmoji.PC:
                    Utility.HardwareDriver.SwitchComputer(EHardwareTrigger.Toggle);
                    break;
                case HardwareEmoji.THUNDER:
                    Utility.HardwareDriver.SwitchGeneral(EHardwareTrigger.Toggle);
                    break;
                case HardwareEmoji.ON:
                    Utility.HardwareDriver.SwitchRoom(EHardwareTrigger.On);
                    break;
                case HardwareEmoji.OFF:
                    Utility.HardwareDriver.SwitchRoom(EHardwareTrigger.Off);
                    break;
                case HardwareEmoji.REBOOT:
                    Utility.HardwareDriver.RebootPC();
                    break;
                default:
                    if (messageStats.UserID != TelegramData.NameToID("@gwynn7")) return;
                    
                    var result = Utility.HardwareDriver.SSH_PC(messageStats.FullMessage, returnResult:true);
                    await cortana.SendMessage(messageStats.ChatID, result);
                    
                    return;
            }
            await cortana.DeleteMessage(messageStats.ChatID, messageStats.MessageID);
        }
        
        public static async void ButtonCallback(ITelegramBotClient cortana, Update update)
        {
            if (!TelegramData.CheckPermission(update.CallbackQuery.From.Id))
            {
                await cortana.AnswerCallbackQuery(update.CallbackQuery.Id);
                return;
            }

            string data = update.CallbackQuery.Data;
            int messageId = update.CallbackQuery.Message.MessageId;
            InlineKeyboardMarkup action;

            if (!HardwareAction.ContainsKey(messageId))
            {
                HardwareAction.Add(messageId, data);
                action = CreateOnOffButtons();
            }
            else
            {
                if (data != "back") Utility.HardwareDriver.SwitchFromString(HardwareAction[messageId], data);
                HardwareAction.Remove(messageId);
                action = CreateHardwareButtons();
            }

            await cortana.AnswerCallbackQuery(update.CallbackQuery.Id);
            await cortana.EditMessageReplyMarkup(update.CallbackQuery.Message.Chat.Id, messageId, action);
        }
        
        private static InlineKeyboardMarkup CreateHardwareButtons()
        {
            InlineKeyboardButton[][] Rows = new InlineKeyboardButton[Enum.GetValues(typeof(EHardwareElements)).Length + 1][];

            Rows[0] = new InlineKeyboardButton[1];
            Rows[0][0] = InlineKeyboardButton.WithCallbackData("Room", "room");

            int index = 1;
            foreach (string element in Enum.GetNames(typeof(EHardwareElements)))
            {
                Rows[index] = new InlineKeyboardButton[1];
                Rows[index][0] = InlineKeyboardButton.WithCallbackData(element, element.ToLower());
                index++;
            }

            InlineKeyboardMarkup hardwareKeyboard = new InlineKeyboardMarkup(Rows);
            return hardwareKeyboard;
        }

        private static InlineKeyboardMarkup CreateOnOffButtons()
        {
            InlineKeyboardButton[][] rows = new InlineKeyboardButton[3][];

            rows[0] = new InlineKeyboardButton[2];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("On", "on");
            rows[0][1] = InlineKeyboardButton.WithCallbackData("Off", "off");

            rows[1] = new InlineKeyboardButton[1];
            rows[1][0] = InlineKeyboardButton.WithCallbackData("Toggle", "toggle");

            rows[2] = new InlineKeyboardButton[1];
            rows[2][0] = InlineKeyboardButton.WithCallbackData("<<", "back");

            InlineKeyboardMarkup keyboard = new InlineKeyboardMarkup(rows);
            return keyboard;
        }

        private static ReplyKeyboardMarkup CreateHardwareToggles()
        {
            var keyboard =
                    new KeyboardButton[][]
                    {
                        [
                            new KeyboardButton(HardwareEmoji.BULB),
                            new KeyboardButton(HardwareEmoji.THUNDER)
                        ],
                        [
                            new KeyboardButton(HardwareEmoji.PC),
                            new KeyboardButton(HardwareEmoji.REBOOT)
                        ],
                        [
                            new KeyboardButton(HardwareEmoji.ON),
                        ],
                        [
                            new KeyboardButton(HardwareEmoji.OFF),
                        ],

                    };
            return new ReplyKeyboardMarkup(keyboard);
        }
    }
}