using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib.API
{
    public enum ProgressModifierType
    {
        /// <summary>
        /// Animation speed stays the same through whole animation.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+x+from+0+to+1">Progress curve</seealso>.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+derivative+x+from+0+to+1">Speed curve</seealso>.
        /// </summary>
        Linear,
        /// <summary>
        /// Animation starts slower and speeds up with time.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+x*x+from+0+to+1">Progress curve</seealso>.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+derivative+x*x+from+0+to+1">Speed curve</seealso>.
        /// </summary>
        Quadratic,
        /// <summary>
        /// Animation starts slower and speeds up with time, but faster then <see cref="Quadratic"/>
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+x*x*x+from+0+to+1">Progress curve</seealso>.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+derivative+x*x*x+from+0+to+1">Speed curve</seealso>.
        /// </summary>
        Cubic,
        /// <summary>
        /// Animation starts much faster and slows down with time.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+sqrt+x+from+0+to+1">Progress curve</seealso>.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+derivative+sqrt+x+from+0+to+1">Speed curve</seealso>.
        /// </summary>
        Sqrt,
        /// <summary>
        /// Starts faster and slows down to zero.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+Sin+x*pi*0.5+from+0+to+1">Progress curve</seealso>.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+derivative+Sin+x*pi*0.5+from+0+to+1">Speed curve</seealso>.
        /// </summary>
        Sin,
        /// <summary>
        /// Starts slower, speeds up and then slows down to zero.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+Sin+x*x*pi*0.5+from+0+to+1">Progress curve</seealso>.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+derivative+Sin+x*x*pi*0.5+from+0+to+1">Speed curve</seealso>.
        /// </summary>
        SinQuadratic,
        /// <summary>
        /// More dramatic version of <see cref="SinQuadratic"/>.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+Sin+x*x*x*x*pi*0.5+from+0+to+1">Progress curve</seealso>.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+derivative+Sin+x*x*x*x*pi*0.5+from+0+to+1">Speed curve</seealso>.
        /// </summary>
        SinQuartic,
        /// <summary>
        /// Starts slow, ends slow, symmetrical.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+0.5-0.5*Cos+x*pi+from+0+to+1">Progress curve</seealso>.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+derivative+0.5-0.5*Cos+x*pi+from+0+to+1">Speed curve</seealso>.
        /// </summary>
        CosShifted,
        /// <summary>
        /// More dramatic version of <see cref="Sqrt"/>.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+sqrt+sqrt+x+from+0+to+1">Progress curve</seealso>.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+derivative+sqrt+sqrt+x+from+0+to+1">Speed curve</seealso>.
        /// </summary>
        SqrtSqrt,
        /// <summary>
        /// Starts slow, ends slow, has a bump at the end that overshoots animation a bit and returns back.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+0.5-0.5*Cos+x*pi+%2B+0.35*Sin^2+x*pi+from+0+to+1">Progress curve</seealso>.
        ///  <seealso href="https://www.wolframalpha.com/input?i=plot+derivative+0.5-0.5*Cos+x*pi+%2B+0.35*Sin^2+x*pi+from+0+to+1">Speed curve</seealso>.
        /// </summary>
        Bounce
    }

    /// <summary>
    /// Stores all the <see cref="ProgressModifiers.ProgressModifier"/> available to animations.
    /// Custom modifiers can be registered.
    /// </summary>
    static public class ProgressModifiers
    {
        public delegate float ProgressModifier(float progress);

        private readonly static Dictionary<ProgressModifierType, ProgressModifier> Modifiers = new()
        {
            { ProgressModifierType.Linear,       (float progress) => progress },
            { ProgressModifierType.Quadratic,    (float progress) => progress * progress },
            { ProgressModifierType.Cubic,        (float progress) => progress * progress * progress },
            { ProgressModifierType.Sqrt,         (float progress) => GameMath.Sqrt(progress) },
            { ProgressModifierType.Sin,          (float progress) => GameMath.Sin(progress / 2 * GameMath.PI) },
            { ProgressModifierType.SinQuadratic, (float progress) => GameMath.Sin(progress * progress / 2 * GameMath.PI) },
            { ProgressModifierType.SinQuartic, (float progress) => GameMath.Sin(progress * progress * progress * progress / 2 * GameMath.PI) },
            { ProgressModifierType.CosShifted,   (float progress) => 0.5f - GameMath.Cos(progress * GameMath.PI) / 2 },
            { ProgressModifierType.SqrtSqrt,     (float progress) => GameMath.Sqrt(GameMath.Sqrt(progress)) },
            { ProgressModifierType.Bounce,       (float progress) => 0.5f - GameMath.Cos(progress * GameMath.PI) / 2 + MathF.Pow(GameMath.Sin(progress * GameMath.PI), 2) * 0.35f },
        };

        public static ProgressModifier Get(ProgressModifierType id) => Modifiers[id];
        public static ProgressModifier Get(int id) => Modifiers[(ProgressModifierType)id];
        public static ProgressModifier Get(string name) => Get((ProgressModifierType)Enum.Parse(typeof(ProgressModifierType), name));
        /// <summary>
        /// Registers <see cref="ProgressModifier"/> by given id.<br/>
        /// It is better to use <see cref="Register(string,ProgressModifier)"/> to avoid conflicts.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="modifier"></param>
        /// <returns><c>false</c> if <paramref name="id"/> already registered</returns>
        public static bool Register(int id, ProgressModifier modifier) => Modifiers.TryAdd((ProgressModifierType)id, modifier);
        /// <summary>
        /// Registers <see cref="ProgressModifier"/> by given name. Name should be unique across mods.
        /// </summary>
        /// <param name="name">Unique name of modifier</param>
        /// <param name="modifier"></param>
        /// <returns><c>false</c> if <paramref name="name"/> already registered, or it has hash conflict with another registered <paramref name="name"/></returns>
        public static bool Register(string name, ProgressModifier modifier) => Modifiers.TryAdd((ProgressModifierType)Utils.ToCrc32(name), modifier);
    }
}
