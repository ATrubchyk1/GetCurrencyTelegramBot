﻿namespace GetCurrencyTelegramBot;

public static class Program
{
    public static void Main(string[] args)
    {
        var currencyBot = new CurrencyBot(ApiConstants.BOT_API);
        currencyBot.CreateCommands();
        currencyBot.StartReceiving();
        Console.ReadKey();
    }
}