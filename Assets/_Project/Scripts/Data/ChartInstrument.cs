namespace RhythmRogue.Data
{
    /// <summary>
    /// Which instrument stem a beat marker was detected in, and the selector an
    /// enemy uses to choose which stream of markers drives its chart.
    ///
    /// On a <see cref="BeatMarker"/>, this is the stem the marker came from. The
    /// generator separates the track into stems (drums / bass / melody) and tags
    /// every marker accordingly, so a kick and a bass note on the same beat become
    /// two separate tagged markers. Markers are never tagged <see cref="All"/>.
    ///
    /// On a selector (<c>EnemyData.chartInstrument</c>), <see cref="All"/> means
    /// "use every marker regardless of stem" (the busiest chart, all instruments
    /// layered), while a specific value keeps only markers from that one stem so the
    /// chart follows that instrument. Because every stem's markers are baked into the
    /// SongBeatMap, this selection is a pure Inspector choice at edit time: no
    /// regeneration is needed to switch which instrument the chart follows.
    /// </summary>
    public enum ChartInstrument
    {
        /// <summary>Selector only: use markers from every stem (all instruments layered).</summary>
        All,

        /// <summary>Percussive stem: kicks, snares, hats. The steady rhythmic backbone.</summary>
        Drums,

        /// <summary>Low end of the harmonic stem: bassline. Sparse, on-the-beat groove.</summary>
        Bass,

        /// <summary>Mid/high of the harmonic stem: lead, vocal, melodic content. Expressive, busier.</summary>
        Melody
    }
}
