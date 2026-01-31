/*  
 *  FILE          : TimeMetrics.cs
 *  PROJECT       : PROG2126 - Assignment - Task TCP/IP Performance
 *  PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli, Mohid Ali
 *  FIRST VERSION : 2026-01-28
 *  DESCRIPTION   :
 *    This file contains simple time-based metrics to support performance experiments.
 */

using System;

namespace Common
{
    public sealed class TimeMetrics
    {
        private long _count;
        private long _totalTicks;
        private long _minTicks;
        private long _maxTicks;

        public TimeMetrics()
        {
            _count = 0;
            _totalTicks = 0;
            _minTicks = long.MaxValue;
            _maxTicks = 0;
        }

        //
        // FUNCTION      : AddSample
        // DESCRIPTION   :
        //   Adds a timing sample (in Stopwatch ticks) to the running metrics.
        // PARAMETERS    :
        //   long elapsedTicks : Sample duration in ticks
        // RETURNS       :
        //   void
        //
        public void AddSample(long elapsedTicks)
        {
            _count = _count + 1;
            _totalTicks = _totalTicks + elapsedTicks;

            if (elapsedTicks < _minTicks)
            {
                _minTicks = elapsedTicks;
            }

            if (elapsedTicks > _maxTicks)
            {
                _maxTicks = elapsedTicks;
            }

            return;
        }

        //
        // FUNCTION      : GetSummary
        // DESCRIPTION   :
        //   Produces a human-readable summary of the collected timing metrics.
        // PARAMETERS    :
        //   double tickFrequency : Stopwatch.Frequency
        // RETURNS       :
        //   string : Summary string
        //
        public string GetSummary(double tickFrequency)
        {
            string summary = string.Empty;

            if (_count > 0)
            {
                double avgTicks = (double)_totalTicks / (double)_count;
                double avgMs = (avgTicks / tickFrequency) * 1000.0;
                double minMs = ((double)_minTicks / tickFrequency) * 1000.0;
                double maxMs = ((double)_maxTicks / tickFrequency) * 1000.0;

                summary =
                    "count=" + _count +
                    ", avgMs=" + avgMs.ToString("F3") +
                    ", minMs=" + minMs.ToString("F3") +
                    ", maxMs=" + maxMs.ToString("F3");
            }
            else
            {
                summary = "count=0";
            }

            return (summary);
        }
    }
}
