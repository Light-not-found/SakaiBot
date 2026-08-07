using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using SakaiBot.Data;
using SakaiBot.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SakaiBot.Commands
{
    public class BirthdaySlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly AppDbContext _dbContext;

        public BirthdaySlashModule(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [SlashCommand("setbirthday", "Set your birthday.")]
        public async Task SetBirthdayAsync(int day, int month, int year)
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a server.", ephemeral: true);
                return;
            }

            var currentYear = DateTime.UtcNow.Year;
            if (year < 1900 || year > currentYear || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                await RespondAsync($"Please provide a valid date. Use a year between 1900 and {currentYear}.", ephemeral: true);
                return;
            }

            var birthDate = new DateOnly(year, month, day);
            var record = await _dbContext.Birthdays
                .FirstOrDefaultAsync(x => x.GuildId == Context.Guild.Id && x.UserId == Context.User.Id);

            if (record is null)
            {
                record = new Birthday
                {
                    GuildId = Context.Guild.Id,
                    UserId = Context.User.Id,
                    BirthDate = birthDate,
                    CreatedAt = DateTime.UtcNow,
                };

                await _dbContext.Birthdays.AddAsync(record);
                await _dbContext.SaveChangesAsync();
                await RespondAsync($"Your birthday has been saved as {birthDate:MMMM d, yyyy}. You are {GetAge(birthDate)} years old.", ephemeral: true);
                return;
            }

            record.BirthDate = birthDate;
            await _dbContext.SaveChangesAsync();
            await RespondAsync($"Your birthday has been updated to {birthDate:MMMM d, yyyy}. You are {GetAge(birthDate)} years old.", ephemeral: true);
        }

        [SlashCommand("getbirthday", "Get your saved birthday.")]
        public async Task GetBirthdayAsync()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a server.", ephemeral: true);
                return;
            }

            var record = await _dbContext.Birthdays
                .FirstOrDefaultAsync(x => x.GuildId == Context.Guild.Id && x.UserId == Context.User.Id);

            if (record is null)
            {
                await RespondAsync("You have not set a birthday yet.", ephemeral: true);
                return;
            }

            await RespondAsync($"Your birthday is {record.BirthDate:MMMM d, yyyy}. You are {GetAge(record.BirthDate)} years old.", ephemeral: true);
        }

        [SlashCommand("nextbirthday", "Show the next birthday in this server.")]
        public async Task NextBirthdayAsync()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a server.", ephemeral: true);
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var birthdays = await _dbContext.Birthdays
                .Where(x => x.GuildId == Context.Guild.Id)
                .ToListAsync();

            var next = birthdays
                .Select(x => new
                {
                    x.UserId,
                    x.BirthDate,
                    NextDate = GetBirthdayDate(today.Year, x.BirthDate) < today
                        ? GetBirthdayDate(today.Year + 1, x.BirthDate)
                        : GetBirthdayDate(today.Year, x.BirthDate)
                })
                .OrderBy(x => x.NextDate)
                .FirstOrDefault();

            if (next is null)
            {
                await RespondAsync("No birthdays found in this server.", ephemeral: true);
                return;
            }

            var user = Context.Guild.GetUser(next.UserId);
            var mention = user?.Mention ?? $"<@{next.UserId}>";
            var daysUntil = next.NextDate.DayNumber - today.DayNumber;
            var dayText = daysUntil == 0 ? "today" : daysUntil == 1 ? "tomorrow" : $"in {daysUntil} days";

            await RespondAsync($"The next birthday is {mention} on {next.BirthDate:MMMM d} ({dayText}).", ephemeral: false);
        }

        [SlashCommand("listbirthdays", "List saved birthdays in this server.")]
        public async Task ListBirthdaysAsync()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a server.", ephemeral: true);
                return;
            }

            var records = await _dbContext.Birthdays
                .Where(x => x.GuildId == Context.Guild.Id)
                .OrderBy(x => x.BirthDate.Month)
                .ThenBy(x => x.BirthDate.Day)
                .ToListAsync();

            if (!records.Any())
            {
                await RespondAsync("There are no saved birthdays in this server yet.", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle($"{Context.Guild.Name} Birthdays")
                .WithDescription($"{records.Count} saved birthday{(records.Count == 1 ? string.Empty : "s")}")
                .WithColor(Color.Gold)
                .WithCurrentTimestamp();

            foreach (var birthday in records)
            {
                var user = Context.Guild.GetUser(birthday.UserId);
                var name = user?.Mention ?? $"<@{birthday.UserId}>";
                embed.AddField(name, $"{birthday.BirthDate:MMMM d, yyyy} | Age: {GetAge(birthday.BirthDate)}", inline: true);
            }

            await RespondAsync(embed: embed.Build(), ephemeral: false);
        }

        [SlashCommand("removebirthday", "Remove your saved birthday.")]
        public async Task RemoveBirthdayAsync()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a server.", ephemeral: true);
                return;
            }

            var record = await _dbContext.Birthdays
                .FirstOrDefaultAsync(x => x.GuildId == Context.Guild.Id && x.UserId == Context.User.Id);

            if (record is null)
            {
                await RespondAsync("You do not have a birthday saved.", ephemeral: true);
                return;
            }

            _dbContext.Birthdays.Remove(record);
            await _dbContext.SaveChangesAsync();
            await RespondAsync("Your birthday has been removed.", ephemeral: true);
        }

        private static int GetAge(DateOnly birthDate)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-age))
            {
                age--;
            }

            return age;
        }

        private static DateOnly GetBirthdayDate(int year, DateOnly birthday)
        {
            if (birthday.Month == 2 && birthday.Day == 29 && !DateTime.IsLeapYear(year))
            {
                return new DateOnly(year, 2, 28);
            }

            return new DateOnly(year, birthday.Month, birthday.Day);
        }
    }
}
