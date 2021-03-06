﻿using PKHeX.Core;
using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class EncounterSettings
    {
        private const string Encounter = nameof(Encounter);
        public override string ToString() => "Encounter Bot Settings";

        [Category(Encounter), Description("The method by which the bot will encounter Pokémon.")]
        public EncounterMode EncounteringType { get; set; } = EncounterMode.VerticalLine;

        [Category(Encounter), Description("Invert the starting direction of movement.")]
        public bool InvertStartingMovement { get; set; } = false;

        [Category(Encounter), Description("Length of the delay in between direction switches in milliseconds")]
        public int SwitchDirectionDelay { get; set; } = 2_400;
    }
}