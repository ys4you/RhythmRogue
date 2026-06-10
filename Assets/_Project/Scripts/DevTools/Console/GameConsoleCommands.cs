#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using RhythmRogue.Util.Console;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Battle;

namespace RhythmRogue.DevTools.Console
{
    /// <summary>
    /// Base for game console commands. Extends the Util ConsoleCommandBase (so it gets POSIX-style
    /// arg parsing and auto -h/--help) and carries the shared DevConsoleContext. Adding a command =
    /// new subclass declaring Name, Help, optional Options, and Run, plus one Register line in
    /// GameConsoleCommands.RegisterAll.
    /// </summary>
    public abstract class GameConsoleCommand : ConsoleCommandBase
    {
        protected readonly DevConsoleContext Ctx;
        protected GameConsoleCommand(DevConsoleContext ctx) { Ctx = ctx; }

        /// <summary>
        /// Reads an integer amount from --amount/-n if present, otherwise the first positional, so
        /// both "heal -n 50" and "heal 50" work. Returns false if neither is a valid integer.
        /// </summary>
        protected static bool TryAmount(ConsoleArgs a, out int value)
        {
            if (a.TryGetInt("amount", out value)) return true;
            if (a.Positionals.Count > 0 && int.TryParse(a.Positionals[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
            value = 0;
            return false;
        }
    }

    /// <summary>Prints run + battle state at a glance.</summary>
    public sealed class StateCommand : GameConsoleCommand
    {
        public StateCommand(DevConsoleContext ctx) : base(ctx) { }
        public override string Name => "state";
        public override string Help => "print run + battle state (seed, beats, hp, relics)";
        public override IReadOnlyList<string> Aliases => new[] { "status" };

        protected override string Run(ConsoleArgs a)
        {
            var sb = new StringBuilder();
            var rs = Ctx.RunState;
            if (rs != null)
            {
                sb.Append($"seed {(string.IsNullOrEmpty(rs.Seed) ? "-" : rs.Seed)}  run {(rs.IsRunActive ? "active" : "idle")}\n");
                sb.Append($"beats {rs.Currency}  relics {rs.ActiveRelics.Count}  battlesWon {rs.BattlesWon}");
            }
            else sb.Append("no RunState loaded");

            var ph = Ctx.Player;
            if (ph != null) sb.Append($"\nplayer HP {ph.CurrentHP}/{ph.MaxHP}");
            var en = Ctx.Enemy;
            if (en != null && en.Health != null) sb.Append($"\nenemy HP {en.CurrentHP}/{en.MaxHP}");
            return sb.ToString();
        }
    }

    /// <summary>Adds currency (negative spends it).</summary>
    public sealed class GiveCommand : GameConsoleCommand
    {
        public GiveCommand(DevConsoleContext ctx) : base(ctx) { }
        public override string Name => "give";
        public override string Help => "add currency (negative spends)";
        public override IReadOnlyList<string> Aliases => new[] { "beats", "money" };
        public override IReadOnlyList<ConsoleOption> Options => new[]
        {
            ConsoleOption.Value('n', "amount", "amount of currency to add (negative spends)"),
        };

        protected override string Run(ConsoleArgs a)
        {
            var rs = Ctx.RunState;
            if (rs == null) return "no RunState loaded.";
            if (!TryAmount(a, out int n)) return "usage: give -n <amount>   (or: give <amount>)";

            if (n >= 0) { rs.AddCurrency(n); return $"+{n} beats (total {rs.Currency})."; }
            bool ok = rs.TrySpendCurrency(-n);
            return ok ? $"{n} beats (total {rs.Currency})." : $"cannot spend {-n}, only {rs.Currency} available.";
        }
    }

    /// <summary>Heals the player; full heal if no amount given.</summary>
    public sealed class HealCommand : GameConsoleCommand
    {
        public HealCommand(DevConsoleContext ctx) : base(ctx) { }
        public override string Name => "heal";
        public override string Help => "heal player (full if no amount)";
        public override IReadOnlyList<ConsoleOption> Options => new[]
        {
            ConsoleOption.Value('n', "amount", "HP to heal (default: full)"),
        };

        protected override string Run(ConsoleArgs a)
        {
            var ph = Ctx.Player;
            if (ph == null) return "no PlayerHealth (start a battle).";
            int amount = TryAmount(a, out int n) ? n : ph.MaxHP;
            ph.Heal(amount);
            return $"player {ph.CurrentHP}/{ph.MaxHP}.";
        }
    }

    /// <summary>Damages the player.</summary>
    public sealed class HurtCommand : GameConsoleCommand
    {
        public HurtCommand(DevConsoleContext ctx) : base(ctx) { }
        public override string Name => "hurt";
        public override string Help => "damage player";
        public override IReadOnlyList<ConsoleOption> Options => new[]
        {
            ConsoleOption.Value('n', "amount", "damage to deal"),
        };

        protected override string Run(ConsoleArgs a)
        {
            var ph = Ctx.Player;
            if (ph == null) return "no PlayerHealth (start a battle).";
            if (!TryAmount(a, out int n)) return "usage: hurt -n <amount>";
            ph.TakeDamage(n);
            return $"player {ph.CurrentHP}/{ph.MaxHP}.";
        }
    }

    /// <summary>Kills the current enemy (instant win).</summary>
    public sealed class WinCommand : GameConsoleCommand
    {
        public WinCommand(DevConsoleContext ctx) : base(ctx) { }
        public override string Name => "win";
        public override string Help => "kill the current enemy";

        protected override string Run(ConsoleArgs a)
        {
            var en = Ctx.Enemy;
            if (en == null || en.Health == null) return "no enemy (not in a battle).";
            en.TakeDamage(en.CurrentHP);
            return "enemy down.";
        }
    }

    /// <summary>Kills the player (instant loss).</summary>
    public sealed class LoseCommand : GameConsoleCommand
    {
        public LoseCommand(DevConsoleContext ctx) : base(ctx) { }
        public override string Name => "lose";
        public override string Help => "kill the player";

        protected override string Run(ConsoleArgs a)
        {
            var ph = Ctx.Player;
            if (ph == null) return "no PlayerHealth (start a battle).";
            ph.TakeDamage(ph.CurrentHP);
            return "player down.";
        }
    }

    /// <summary>Gets or sets Time.timeScale.</summary>
    public sealed class TimeScaleCommand : GameConsoleCommand
    {
        public TimeScaleCommand(DevConsoleContext ctx) : base(ctx) { }
        public override string Name => "time";
        public override string Help => "get or set Time.timeScale";
        public override IReadOnlyList<ConsoleOption> Options => new[]
        {
            ConsoleOption.Value('s', "scale", "new time scale, e.g. 0.5"),
        };

        protected override string Run(ConsoleArgs a)
        {
            bool has = a.TryGetFloat("scale", out float scale);
            if (!has && a.Positionals.Count > 0)
                has = float.TryParse(a.Positionals[0], NumberStyles.Float, CultureInfo.InvariantCulture, out scale);
            if (!has) return $"timeScale {Time.timeScale:F2}.";

            Time.timeScale = Mathf.Clamp(scale, 0f, 8f);
            return $"timeScale {Time.timeScale:F2}.";
        }
    }

    /// <summary>Owns / disowns / lists relics. Effects apply at the next battle start.</summary>
    public sealed class RelicCommand : GameConsoleCommand, IArgumentCompleter
    {
        public RelicCommand(DevConsoleContext ctx) : base(ctx) { }
        public override string Name => "relic";
        public override string Help => "manage owned relics: list | add <name> | remove <name> | clear";

        protected override string Run(ConsoleArgs a)
        {
            var rs = Ctx.RunState;
            if (rs == null) return "no RunState loaded.";
            var pool = Ctx.RelicPool;
            var pos = a.Positionals;
            string sub = pos.Count > 0 ? pos[0].ToLowerInvariant() : "list";

            if (sub == "list")
            {
                if (pool == null) return "no RelicPool loaded.";
                var sb = new StringBuilder($"relics ({rs.ActiveRelics.Count} owned, effects apply next battle):");
                foreach (var r in pool.AllRelics)
                {
                    if (r == null) continue;
                    sb.Append('\n').Append(rs.ActiveRelics.Contains(r) ? "  [x] " : "  [ ] ").Append(r.relicName);
                }
                return sb.ToString();
            }

            if (sub == "clear") { rs.ActiveRelics.Clear(); return "all relics cleared."; }

            if (sub == "add" || sub == "remove")
            {
                if (pool == null) return "no RelicPool loaded.";
                if (pos.Count < 2) return $"usage: relic {sub} <name>";

                var nameBuilder = new StringBuilder();
                for (int i = 1; i < pos.Count; i++) { if (i > 1) nameBuilder.Append(' '); nameBuilder.Append(pos[i]); }
                string query = nameBuilder.ToString();

                RelicData match = FindRelic(pool, query);
                if (match == null) return $"no relic matching '{query}'. try 'relic list'.";

                bool owned = rs.ActiveRelics.Contains(match);
                if (sub == "add")
                {
                    if (owned) return $"{match.relicName} already owned.";
                    rs.ActiveRelics.Add(match);
                    return $"added {match.relicName} (effect applies next battle).";
                }
                if (!owned) return $"{match.relicName} not owned.";
                rs.ActiveRelics.Remove(match);
                return $"removed {match.relicName}.";
            }

            return Usage();
        }

        // Tab-completion: subcommands first, then relic names for add/remove.
        public IEnumerable<string> CompleteArguments(string[] priorArgs, string partial)
        {
            if (priorArgs == null || priorArgs.Length == 0)
                return new[] { "list", "add", "remove", "clear" };

            string sub = priorArgs[0].ToLowerInvariant();
            if ((sub == "add" || sub == "remove") && Ctx.RelicPool != null)
            {
                var names = new List<string>();
                foreach (var r in Ctx.RelicPool.AllRelics)
                    if (r != null && !string.IsNullOrEmpty(r.relicName)) names.Add(r.relicName);
                return names;
            }
            return System.Array.Empty<string>();
        }

        // Exact (case-insensitive) name wins; otherwise the first partial match.
        private static RelicData FindRelic(RelicPool pool, string query)
        {
            RelicData partial = null;
            foreach (var r in pool.AllRelics)
            {
                if (r == null || string.IsNullOrEmpty(r.relicName)) continue;
                if (string.Equals(r.relicName, query, System.StringComparison.OrdinalIgnoreCase)) return r;
                if (partial == null && r.relicName.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0) partial = r;
            }
            return partial;
        }
    }

    /// <summary>
    /// One place to register every game command. New command = new GameConsoleCommand subclass
    /// above + one line here. Nothing else in the console changes.
    /// </summary>
    public static class GameConsoleCommands
    {
        public static void RegisterAll(DeveloperConsole console, DevConsoleContext ctx)
        {
            console.Register(new StateCommand(ctx));
            console.Register(new GiveCommand(ctx));
            console.Register(new HealCommand(ctx));
            console.Register(new HurtCommand(ctx));
            console.Register(new WinCommand(ctx));
            console.Register(new LoseCommand(ctx));
            console.Register(new TimeScaleCommand(ctx));
            console.Register(new RelicCommand(ctx));
        }
    }
}
#endif
