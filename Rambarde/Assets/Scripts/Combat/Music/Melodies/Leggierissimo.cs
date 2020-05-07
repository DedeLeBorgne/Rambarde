﻿using System.Threading.Tasks;
using Characters;
using Melodies;
using Status;
using UnityEngine;

namespace Music.Melodies
{
    [CreateAssetMenu(fileName = "Leggierissimo", menuName = "Melody/Leggierissimo")]
    class Leggierissimo : Melody
    {
        protected override async Task ExecuteOnTarget(CharacterControl t)
        {
            await CombatManager.Instance.dialogManager.ShowDialog(DialogFilter.Heal,
                CharacterType.Bard, CharacterType.None);
                            
            await t.Heal(10);
                            
            await CombatManager.Instance.dialogManager.ShowDialog(DialogFilter.Heal,
                CharacterType.None, Dialog.GetCharacterTypeFromCharacterControl(t));
            // Heal Poison, Confused, Destabilised
            Debug.Log("Annule tous les changements de stats de: " + t.name);
        }
    }
}