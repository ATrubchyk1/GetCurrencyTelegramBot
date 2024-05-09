using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GetCurrencyTelegramBot;

public class CurrencyBot
{
    private readonly TelegramBotClient _telegramBotClient;

    private readonly List<string> _currencyCodes = new()
    {
        CurrencyCode.BTC, CurrencyCode.BNB, CurrencyCode.ETH, CurrencyCode.DOT, CurrencyCode.TON, CurrencyCode.SOL
    };

    private readonly List<string> _currencyCustomCommands = new()
    {
        CustomBotCommands.SHOW_CURRENCIES, CustomBotCommands.START
    };
    
    public CurrencyBot(string token)
    {
        _telegramBotClient = new TelegramBotClient(token);
    }

    public void CreateCommands()
    {
        // Создаем список и описание меню команд бота
        _telegramBotClient.SetMyCommandsAsync(new List<BotCommand>()
        {
            new()
            {
                Command = CustomBotCommands.START,
                Description = "Запуск бота."
            },
            new()
            {
                Command = CustomBotCommands.SHOW_CURRENCIES,
                Description = "Вывод сообщения с выбором 1 из 4 валют, для получения ее цены в данный момент."
            }
        });
    }
    
    public void StartReceiving()
    {
    
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        // Создаем список обрабатываемых типов сообщений
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new UpdateType[]
            {
                UpdateType.Message, UpdateType.CallbackQuery
            }
        };

        // Начинаем отслеживание сообщений от пользователя
        _telegramBotClient.StartReceiving(
            HandleUpdateAsync,
            HandleError,
            receiverOptions,
            cancellationToken);
    }

    private Task HandleError(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(exception);
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        switch (update.Type)
        {
            case UpdateType.Message:
                await HandleMessageAsync(update, cancellationToken);
                break;
            case UpdateType.CallbackQuery:
                await HandleCallbackQueryAsync(update, cancellationToken);
                break;
        }
    }

    private async Task HandleMessageAsync(Update update, CancellationToken cancellationToken)
    {
        if (update.Message == null)
        {
            return;
        }
        var chatId = update.Message.Chat.Id;
        await DeleteMessage(chatId, update.Message.MessageId, cancellationToken);
        if (!_currencyCustomCommands.Contains(update.Message.Text))
        {
            await _telegramBotClient.SendTextMessageAsync(chatId: chatId,
                text: "Бот принимает только команды из меню.",
                cancellationToken: cancellationToken);
            return;
        }

        var messageText = update.Message.Text;
        // Проверяем если пришла команда старт то отправляем стартовое сообщение
        if (IsStartCommand(messageText))
        {
            await SendStartMessageAsync(chatId, cancellationToken);
            return;
        }

        if (IsShowCommand(messageText))
        {
            await ShowCurrencySelectionAsync(chatId, cancellationToken);
        }
    }
    
    private async Task DeleteMessage(long chatId, int messageId, CancellationToken cancellationToken)
    {
        try
        {
            await _telegramBotClient.DeleteMessageAsync(chatId, messageId, cancellationToken);
        }
        catch (ApiRequestException exception)
        {
            // В случае ошибки с кодом 400 (Сообщение удалено), выводим сообщение в консоль.
            if (exception.ErrorCode == 400)
            {
                Console.WriteLine("User deleted message");
            }
        }
    }

    private bool IsStartCommand(string messageText)
    {
        return messageText.ToLower() == CustomBotCommands.START;
    }
    
    private bool IsShowCommand(string messageText)
    {
        return messageText.ToLower() == CustomBotCommands.SHOW_CURRENCIES;
    }
    
    private async Task SendStartMessageAsync(long? chatId, CancellationToken cancellationToken)
    {
        // Массив с онлайн кнопкой с колбэком ответа на стартовое сообщение
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Выбрать валюту.",
                    CustomCallbackData.SHOW_CURRENCIES_MENU)
            }
        });

        // Оправка сообщения с онлайн кнопкой
        await _telegramBotClient.SendTextMessageAsync(chatId, 
            "Привет!\n" + "Данный бот показывает текущий курс выбранной валюты.\n",
            replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }

    private async Task ShowCurrencySelectionAsync(long? chatId, CancellationToken cancellationToken)
    {
        // Массив с онлайн кнопками
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            // Строка 1
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Bitcoin", CurrencyCode.BTC),
                InlineKeyboardButton.WithCallbackData("Ethereum", CurrencyCode.ETH),
                InlineKeyboardButton.WithCallbackData("Toncoin",CurrencyCode.TON)
            },
            // Строка 2
            new[]
            {
                InlineKeyboardButton.WithCallbackData("BNB", CurrencyCode.BNB),
                InlineKeyboardButton.WithCallbackData("Polkadot", CurrencyCode.DOT),
                InlineKeyboardButton.WithCallbackData("Solana", CurrencyCode.SOL)
            }
        });

        await _telegramBotClient.SendTextMessageAsync(chatId: chatId, text: "Выберите валюту",
            replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(Update update, CancellationToken cancellationToken)
    {
        if (update.CallbackQuery?.Message == null)
        {
            return;
        }

        var chatId = update.CallbackQuery.Message.Chat.Id;
        var callbackData = update.CallbackQuery.Data;
        var messageId = update.CallbackQuery.Message.MessageId;
        
        // Проверяем пользователь нажал на oнлайн кнопку в ответ на стартовое сообщение
        if (callbackData is CustomCallbackData.SHOW_CURRENCIES_MENU or CustomCallbackData.RETURN_TO_CURRENCIES_MENU)
        {
            await DeleteMessage(chatId, messageId, cancellationToken);
            await ShowCurrencySelectionAsync(chatId, cancellationToken);
            return;
        }
        
        // Проверяем коллбэк - это код валюты
        if (_currencyCodes.Contains(callbackData))
        {
            await DeleteMessage(chatId, messageId, cancellationToken);
            await SendCurrencyPriceAsync(chatId, callbackData, cancellationToken);
            return;
        }
        
        // Проверяем пользователь нажал на инлайн кнопку сменить валюту
        if (callbackData == CustomCallbackData.RETURN_TO_CURRENCIES_MENU)
        {
            await ShowCurrencySelectionAsync(chatId, cancellationToken);
        }
    }
    
    private async Task SendCurrencyPriceAsync(long? chatId, string currencyCode, CancellationToken cancellationToken)
    {
        var data = await CoinMarket.GetPriceAsync(currencyCode);
        
        // Создаем инлайн кнопку с коллбэком для смены валюты
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Выбрать другую валюту.",
                    CustomCallbackData.RETURN_TO_CURRENCIES_MENU)
            }
        });

        // Отправляем сообщение с инлайн кнопкой
        await _telegramBotClient.SendTextMessageAsync(chatId,
            text: $"Валюта: {currencyCode}, стоимость: {Math.Round(data[0], 3)}$ \n" 
            + $"Рыночная капитализация: {Math.Round(data[1])}$",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }
}