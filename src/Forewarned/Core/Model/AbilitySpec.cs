namespace Forewarned.Core.Model
{
    /// <summary>Urgency (PLAN.md §9). Ordered: a higher value is more urgent.</summary>
    public enum Level { None, Info, Caution, Danger }

    /// <summary>
    /// One boss ability: our decisions (level, wording, response, shape kind) plus fallback numbers
    /// from the offline research (PLAN.md §4, §10). Live values from the game override the numbers.
    /// </summary>
    public sealed class AbilitySpec
    {
        /// <summary>Stable id, also the translation-key prefix: "fader.flamebreath".</summary>
        public string Id;
        /// <summary>English display name for config rows; the shown name comes from NameKey.</summary>
        public string Name;
        /// <summary>The boss inventory item that carries this attack: "Fader_Flamebreath".</summary>
        public string ItemPrefab;
        /// <summary>Animator triggers that start it, including any index the game appends.</summary>
        public string[] Triggers = new string[0];
        public Level DefaultLevel;
        public bool DefaultOn = true;
        /// <summary>Translation key of the action line ("action.get_behind") or announce text. Null for Level.None.</summary>
        public string ActionKey;
        public Response Response;
        public Shape Shape = Shape.None;
        public float Cooldown;
        public float HpMin;
        public float HpMax = 1f;
        /// <summary>Seconds from trigger to the first hit.</summary>
        public float WindUp;
        public int Hits = 1;
        public float AiRange;
        public float MaxAngle;

        public string NameKey => Id + ".name";
    }

    /// <summary>Numbers read from the boss's own item in game (PLAN.md §11.3). Null: not available.</summary>
    public sealed class AbilityNumbers
    {
        public float? Cooldown;
        public float? HpMin;
        public float? HpMax;
        public float? AiRange;
        public float? MaxAngle;
        public float? Range;
        public float? Angle;
        public float? Width;
        public float? Radius;
        public float? Offset;
    }

    /// <summary>A spec with live numbers and the learned wind-up applied: what a warning is built from.</summary>
    public sealed class ResolvedAbility
    {
        public AbilitySpec Spec;
        public Shape Shape;
        public float Cooldown;
        public float HpMin;
        public float HpMax;
        public float AiRange;
        public float MaxAngle;
        public float WindUp;
        public bool WindUpLearned;

        public static ResolvedAbility Resolve(AbilitySpec spec, AbilityNumbers live, float? learnedWindUp)
        {
            live = live ?? new AbilityNumbers();
            Shape shape = spec.Shape.Copy();
            if (shape.Source != ShapeSource.Fixed)
            {
                shape.Range = live.Range ?? shape.Range;
                shape.Angle = live.Angle ?? shape.Angle;
                shape.Width = live.Width ?? shape.Width;
                shape.Radius = live.Radius ?? shape.Radius;
                shape.Offset = live.Offset ?? shape.Offset;
            }
            return new ResolvedAbility
            {
                Spec = spec,
                Shape = shape,
                Cooldown = live.Cooldown ?? spec.Cooldown,
                HpMin = live.HpMin ?? spec.HpMin,
                HpMax = live.HpMax ?? spec.HpMax,
                AiRange = live.AiRange ?? spec.AiRange,
                MaxAngle = live.MaxAngle ?? spec.MaxAngle,
                WindUp = learnedWindUp ?? spec.WindUp,
                WindUpLearned = learnedWindUp.HasValue
            };
        }

        /// <summary>BaseAI.CanUseAttack's health gates: both bounds inclusive (PLAN.md §1).</summary>
        public bool UsableAt(float hp) => hp >= HpMin - 1e-4f && hp <= HpMax + 1e-4f;
    }
}
