namespace RhythmRogue.Data
{
    /// <summary>
    /// Controls how ChartAssembler fills a chart to match the song duration.
    /// 
    /// Set on ChartTemplate to define the looping strategy:
    ///   None      — stamp sections once, chart ends when sections end
    ///   LoopAll   — repeat all sections until target duration is reached
    ///   LoopRange — sections before loopStartIndex play once (intro),
    ///               sections in [loopStart, loopEnd] repeat to fill,
    ///               sections after loopEndIndex play once (outro)
    /// </summary>
    public enum RepeatMode
    {
        /// <summary>No looping. Sections play once. Chart may be shorter than song.</summary>
        None,

        /// <summary>Repeat all sections until the target duration is reached.</summary>
        LoopAll,

        /// <summary>
        /// Repeat a range of sections. Sections outside the range play once.
        /// Use loopStartIndex and loopEndIndex on ChartTemplate to define the range.
        /// </summary>
        LoopRange
    }
}