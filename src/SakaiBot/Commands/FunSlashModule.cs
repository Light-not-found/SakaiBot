using Discord.Interactions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SakaiBot.Commands
{
    public class FunSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
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

        [SlashCommand("blackjack", "Start a game of blackjack.")]
        public async Task BlackjackAsync()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("Blackjack can only be played in a server.", ephemeral: true);
                return;
            }

            var key = (Context.Guild.Id, Context.User.Id);
            if (BlackjackGames.ContainsKey(key))
            {
                await RespondAsync("You already have a blackjack game in progress. Use `/blackjack-hit` or `/blackjack-stand`.", ephemeral: true);
                return;
            }

            var game = BlackjackGame.Create();
            BlackjackGames[key] = game;

            if (game.PlayerValue == 21)
            {
                BlackjackGames.TryRemove(key, out _);
                await RespondAsync(FormatResult("Blackjack! You win.", game, revealDealer: true), ephemeral: false);
                return;
            }

            await RespondAsync(FormatGame("Your move. Use `/blackjack-hit` or `/blackjack-stand`.", game), ephemeral: false);
        }

        [SlashCommand("blackjack-hit", "Draw another card in your blackjack game.")]
        public async Task BlackjackHitAsync()
        {
            if (!TryGetGame(out var key, out var game))
            {
                await RespondAsync("You do not have a blackjack game in progress. Use `/blackjack` to start one.", ephemeral: true);
                return;
            }

            game.PlayerHand.Add(game.DrawCard());
            if (game.PlayerValue > 21)
            {
                BlackjackGames.TryRemove(key, out _);
                await RespondAsync(FormatResult("Bust. The dealer wins.", game, revealDealer: true), ephemeral: false);
                return;
            }

            await RespondAsync(FormatGame("Your move. Use `/blackjack-hit` or `/blackjack-stand`.", game), ephemeral: false);
        }

        [SlashCommand("blackjack-stand", "Stop drawing and let the dealer play.")]
        public async Task BlackjackStandAsync()
        {
            if (!TryGetGame(out var key, out var game))
            {
                await RespondAsync("You do not have a blackjack game in progress. Use `/blackjack` to start one.", ephemeral: true);
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

            BlackjackGames.TryRemove(key, out _);
            await RespondAsync(FormatResult(message, game, revealDealer: true), ephemeral: false);
        }

        [SlashCommand("fortune", "Get a random fortune.")]
        public async Task FortuneAsync()
        {
            var random = new Random();
            var fortune = Fortunes[random.Next(Fortunes.Length)];
            await RespondAsync($"🔮 {fortune}", ephemeral: false);
        }

        [SlashCommand("meme", "Get a random meme caption idea.")]
        public async Task MemeAsync()
        {
            var random = new Random();
            var captions = new[]
            {
                "When the bot is online but the server still sleeps.",
                "That moment when your code compiles on the first try.",
                "Me: I'll just write one more feature.",
                "When the birthday reminder hits exactly on time.",
                "Discord: 4014. Bot: I only want to moderate.",
                "Roll initiative! The DM says it's going to be a wild night.",
                "When the bot sees the bad word and reacts with a timeout.",
            };

            await RespondAsync($"{captions[random.Next(captions.Length)]}", ephemeral: false);
        }

        private bool TryGetGame(out (ulong GuildId, ulong UserId) key, out BlackjackGame game)
        {
            key = (Context.Guild?.Id ?? 0, Context.User.Id);
            return BlackjackGames.TryGetValue(key, out game!);
        }

        private static string FormatGame(string message, BlackjackGame game)
            => $"{message}\n\n**Your hand:** {FormatHand(game.PlayerHand)} = **{game.PlayerValue}**\n**Dealer:** {game.DealerHand[0]} and **hidden**";

        private static string FormatResult(string message, BlackjackGame game, bool revealDealer)
            => $"{message}\n\n**Your hand:** {FormatHand(game.PlayerHand)} = **{game.PlayerValue}**\n**Dealer's hand:** {FormatHand(game.DealerHand)} = **{game.DealerValue}";

        private static string FormatHand(IEnumerable<Card> hand)
            => string.Join(", ", hand.Select(card => card.ToString()));

        private sealed class BlackjackGame
        {
            private readonly Queue<Card> _deck;

            public List<Card> PlayerHand { get; } = new();
            public List<Card> DealerHand { get; } = new();

            public int PlayerValue => CalculateValue(PlayerHand);
            public int DealerValue => CalculateValue(DealerHand);

            private BlackjackGame(Queue<Card> deck)
            {
                _deck = deck;
            }

            public static BlackjackGame Create()
            {
                var cards = new List<Card>();
                foreach (var suit in new[] { "Hearts", "Diamonds", "Clubs", "Spades" })
                {
                    foreach (var rank in new[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" })
                    {
                        cards.Add(new Card(rank, suit));
                    }
                }

                for (var index = cards.Count - 1; index > 0; index--)
                {
                    var swapIndex = Random.Shared.Next(index + 1);
                    (cards[index], cards[swapIndex]) = (cards[swapIndex], cards[index]);
                }

                var game = new BlackjackGame(new Queue<Card>(cards));
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
