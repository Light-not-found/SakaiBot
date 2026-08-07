using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using SakaiBot.Data;
using SakaiBot.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SakaiBot.Commands
{
    public class FunSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly AppDbContext _dbContext;
        private static readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), BlackjackGame> BlackjackGames = new();

        private static readonly string[] Fortunes = new[]
        {
            "Today is a great day for a new beginning.",
            "Someone will make you smile soon.",
            "A surprise gift is heading your way.",
            "Your hard work will pay off in unexpected ways.",
            "Take a chance today; it could change everything.",
            "A fun new friend may appear in your life.",
            "You are closer to your goal than you think.",
            "Good news will arrive from an old connection.",
            "Be bold and creative; others will notice.",
            "A small act of kindness will come back to you."
        };

        public FunSlashModule(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [SlashCommand("blackjack", "Start a game of blackjack.")]
        public async Task BlackjackAsync(int bet = 100)
        {
            if (Context.Guild is null)
            {
                await RespondAsync("Blackjack can only be played in a server.", ephemeral: true);
                return;
            }

            var key = (Context.Guild.Id, Context.User.Id);
            if (bet < 10 || bet > 100000)
            {
                await RespondAsync("Your bet must be between 10 and 100,000 credits.", ephemeral: true);
                return;
            }

            if (BlackjackGames.ContainsKey(key))
            {
                await RespondAsync("You already have a blackjack game in progress. Use the buttons on your current game.", ephemeral: true);
                return;
            }

            var account = await GetAccountAsync(Context.Guild.Id, Context.User.Id);
            if (account.Balance < bet)
            {
                await RespondAsync($"You need {bet:N0} credits, but your balance is {account.Balance:N0}.", ephemeral: true);
                return;
            }

            account.Balance -= bet;
            await _dbContext.SaveChangesAsync();
            var game = BlackjackGame.Create(bet);
            BlackjackGames[key] = game;

            if (game.PlayerValue == 21)
            {
                BlackjackGames.TryRemove(key, out _);
                account.Balance += (int)(bet * 2.5m);
                await _dbContext.SaveChangesAsync();
                await RespondAsync(embed: BuildGameEmbed("Blackjack! You win 2.5x your bet.", game, revealDealer: true, Color.Green), components: BuildButtons(Context.User.Id, disabled: true), ephemeral: false);
                return;
            }

            await RespondAsync(embed: BuildGameEmbed("Your move. Choose Hit or Stand.", game, revealDealer: false, Color.Blue), components: BuildButtons(Context.User.Id, disabled: false), ephemeral: false);
        }

        [ComponentInteraction("blackjack-hit:*")]
        public async Task BlackjackHitButtonAsync(string ownerId)
        {
            if (!TryGetGame(ownerId, out var key, out var game))
            {
                await RespondAsync("This blackjack game belongs to another player or has ended.", ephemeral: true);
                return;
            }

            game.PlayerHand.Add(game.DrawCard());
            if (game.PlayerValue > 21)
            {
                BlackjackGames.TryRemove(key, out _);
                await UpdateGameAsync("Bust. The dealer wins.", game, revealDealer: true, Color.Red, disabled: true);
                return;
            }

            await UpdateGameAsync("Your move. Choose Hit or Stand.", game, revealDealer: false, Color.Blue, disabled: false);
        }

        [ComponentInteraction("blackjack-stand:*")]
        public async Task BlackjackStandButtonAsync(string ownerId)
        {
            if (!TryGetGame(ownerId, out var key, out var game))
            {
                await RespondAsync("This blackjack game belongs to another player or has ended.", ephemeral: true);
                return;
            }

            while (game.DealerValue < 17)
            {
                game.DealerHand.Add(game.DrawCard());
            }

            var message = game.DealerValue > 21
                ? "The dealer busts. You win!"
                : game.PlayerValue > game.DealerValue
                    ? "You win!"
                    : game.PlayerValue == game.DealerValue
                        ? "Push. It is a tie."
                        : "The dealer wins.";

            var account = await GetAccountAsync(key.GuildId, key.UserId);
            if (game.DealerValue > 21 || game.PlayerValue > game.DealerValue)
            {
                account.Balance += game.Bet * 2;
            }
            else if (game.PlayerValue == game.DealerValue)
            {
                account.Balance += game.Bet;
            }

            await _dbContext.SaveChangesAsync();

            BlackjackGames.TryRemove(key, out _);
            await UpdateGameAsync(message, game, revealDealer: true, game.DealerValue > 21 || game.PlayerValue > game.DealerValue ? Color.Green : Color.Red, disabled: true);
        }

        [ComponentInteraction("blackjack-new:*")]
        public async Task BlackjackNewButtonAsync(string ownerId)
        {
            if (!ulong.TryParse(ownerId, out var userId) || userId != Context.User.Id)
            {
                await RespondAsync("This blackjack game belongs to another player.", ephemeral: true);
                return;
            }

            var key = (Context.Guild?.Id ?? 0, userId);
            var account = await GetAccountAsync(key.Item1, userId);
            const int defaultBet = 100;
            if (account.Balance < defaultBet)
            {
                await RespondAsync($"You need {defaultBet:N0} credits to start a new game.", ephemeral: true);
                return;
            }

            account.Balance -= defaultBet;
            await _dbContext.SaveChangesAsync();
            var game = BlackjackGame.Create(defaultBet);
            BlackjackGames[key] = game;

            await UpdateGameAsync(game.PlayerValue == 21 ? "Blackjack! You win." : "Your move. Choose Hit or Stand.", game, game.PlayerValue == 21, game.PlayerValue == 21 ? Color.Green : Color.Blue, disabled: game.PlayerValue == 21);
            if (game.PlayerValue == 21)
            {
                BlackjackGames.TryRemove(key, out _);
            }
        }

        [SlashCommand("fortune", "Get a random fortune.")]
        public async Task FortuneAsync()
        {
            var random = new Random();
            var fortune = Fortunes[random.Next(Fortunes.Length)];
            await RespondAsync($"🔮 {fortune}", ephemeral: false);
        }

        [SlashCommand("balance", "Show your virtual credit balance.")]
        public async Task BalanceAsync()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a server.", ephemeral: true);
                return;
            }

            var account = await GetAccountAsync(Context.Guild.Id, Context.User.Id);
            await RespondAsync($"You have **{account.Balance:N0} credits**.", ephemeral: true);
        }

        [SlashCommand("daily", "Claim your daily virtual credits.")]
        public async Task DailyAsync()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a server.", ephemeral: true);
                return;
            }

            var account = await GetAccountAsync(Context.Guild.Id, Context.User.Id);
            if (account.LastDailyClaimedAt.HasValue && DateTime.UtcNow - account.LastDailyClaimedAt.Value < TimeSpan.FromHours(24))
            {
                var remaining = TimeSpan.FromHours(24) - (DateTime.UtcNow - account.LastDailyClaimedAt.Value);
                await RespondAsync($"You already claimed your daily reward. Try again in {remaining.Hours}h {remaining.Minutes}m.", ephemeral: true);
                return;
            }

            account.Balance += 500;
            account.LastDailyClaimedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await RespondAsync($"You claimed **500 credits**. Balance: **{account.Balance:N0}**.", ephemeral: true);
        }

        [SlashCommand("blackjackleaderboard", "Show the richest players in this server.")]
        public async Task BlackjackLeaderboardAsync()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a server.", ephemeral: true);
                return;
            }

            var accounts = await _dbContext.EconomyAccounts
                .AsNoTracking()
                .Where(x => x.GuildId == Context.Guild.Id)
                .OrderByDescending(x => x.Balance)
                .Take(10)
                .ToListAsync();

            var embed = new EmbedBuilder()
                .WithTitle($"{Context.Guild.Name} Credit Leaderboard")
                .WithColor(Color.Gold);

            if (accounts.Count == 0)
            {
                embed.WithDescription("No players have an economy account yet.");
            }
            else
            {
                for (var index = 0; index < accounts.Count; index++)
                {
                    var user = Context.Guild.GetUser(accounts[index].UserId);
                    embed.AddField($"#{index + 1} {user?.Username ?? $"<@{accounts[index].UserId}>"}", $"{accounts[index].Balance:N0} credits", inline: false);
                }
            }

            await RespondAsync(embed: embed.Build(), ephemeral: false);
        }

        private async Task<EconomyAccount> GetAccountAsync(ulong guildId, ulong userId)
        {
            var account = await _dbContext.EconomyAccounts.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);
            if (account is null)
            {
                account = new EconomyAccount { GuildId = guildId, UserId = userId };
                await _dbContext.EconomyAccounts.AddAsync(account);
                await _dbContext.SaveChangesAsync();
            }

            return account;
        }

        private bool TryGetGame(string ownerId, out (ulong GuildId, ulong UserId) key, out BlackjackGame game)
        {
            key = (Context.Guild?.Id ?? 0, Context.User.Id);
            if (!ulong.TryParse(ownerId, out var ownerUserId) || ownerUserId != Context.User.Id)
            {
                game = null!;
                return false;
            }

            return BlackjackGames.TryGetValue(key, out game!);
        }

        private async Task UpdateGameAsync(string message, BlackjackGame game, bool revealDealer, Color color, bool disabled)
        {
            await DeferAsync();
            await ModifyOriginalResponseAsync(properties =>
            {
                properties.Embed = BuildGameEmbed(message, game, revealDealer, color);
                properties.Components = BuildButtons(Context.User.Id, disabled);
            });
        }

        private static Embed BuildGameEmbed(string message, BlackjackGame game, bool revealDealer, Color color)
        {
            var embed = new EmbedBuilder()
                .WithTitle("Blackjack")
                .WithDescription(message)
                .WithColor(color)
                .AddField("Your hand", $"{FormatHand(game.PlayerHand)}\nValue: **{game.PlayerValue}**")
                .WithCurrentTimestamp();

            embed.AddField("Dealer's hand", revealDealer
                ? $"{FormatHand(game.DealerHand)}\nValue: **{game.DealerValue}**"
                : $"{game.DealerHand[0]} and **hidden**");

            return embed.Build();
        }

        private static MessageComponent BuildButtons(ulong userId, bool disabled)
            => new ComponentBuilder()
                .WithButton("Hit", $"blackjack-hit:{userId}", ButtonStyle.Primary, disabled: disabled)
                .WithButton("Stand", $"blackjack-stand:{userId}", ButtonStyle.Success, disabled: disabled)
                .WithButton("New Game", $"blackjack-new:{userId}", ButtonStyle.Secondary)
                .Build();

        private static string FormatHand(IEnumerable<Card> hand)
            => string.Join(", ", hand.Select(card => card.ToString()));

        private sealed class BlackjackGame
        {
            private readonly Queue<Card> _deck;

            public List<Card> PlayerHand { get; } = new();
            public List<Card> DealerHand { get; } = new();

            public int PlayerValue => CalculateValue(PlayerHand);
            public int DealerValue => CalculateValue(DealerHand);
            public int Bet { get; }

            private BlackjackGame(Queue<Card> deck, int bet)
            {
                _deck = deck;
                Bet = bet;
            }

            public static BlackjackGame Create(int bet)
            {
                var cards = new List<Card>();
                for (var deck = 0; deck < 4; deck++)
                {
                    foreach (var suit in new[] { "Hearts", "Diamonds", "Clubs", "Spades" })
                    {
                        foreach (var rank in new[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" })
                        {
                            cards.Add(new Card(rank, suit));
                        }
                    }
                }

                for (var index = cards.Count - 1; index > 0; index--)
                {
                    var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
                    (cards[index], cards[swapIndex]) = (cards[swapIndex], cards[index]);
                }

                var game = new BlackjackGame(new Queue<Card>(cards), bet);
                game.PlayerHand.Add(game.DrawCard());
                game.DealerHand.Add(game.DrawCard());
                game.PlayerHand.Add(game.DrawCard());
                game.DealerHand.Add(game.DrawCard());
                return game;
            }

            public Card DrawCard() => _deck.Dequeue();

            private static int CalculateValue(IEnumerable<Card> hand)
            {
                var value = hand.Sum(card => card.Value);
                var aces = hand.Count(card => card.Rank == "A");
                while (value > 21 && aces-- > 0)
                {
                    value -= 10;
                }

                return value;
            }
        }

        private sealed record Card(string Rank, string Suit)
        {
            public int Value => Rank switch
            {
                "A" => 11,
                "J" or "Q" or "K" => 10,
                _ => int.Parse(Rank)
            };

            public override string ToString() => $"{Rank} of {Suit}";
        }
    }
}
