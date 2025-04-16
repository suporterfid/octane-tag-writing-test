using Impinj.OctaneSdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctaneTagWritingTest.JobStrategies
{
    /// <summary>
    /// Class to track the processing state of a tag throughout the workflow
    /// </summary>
    public class TagProcessingInfo
    {
        // The original Tag object
        public Tag Tag { get; set; }

        // Original EPC read during collection phase
        public string OriginalEpc { get; set; }

        // EPC we've assigned and are trying to write
        public string ExpectedEpc { get; set; }

        // EPC read during verification phase
        public string VerifiedEpc { get; set; }

        // Timers for measuring operations
        public Stopwatch WriteTimer { get; } = new Stopwatch();
        public Stopwatch VerifyTimer { get; } = new Stopwatch();

        // Status flags
        public bool IsWritten { get; set; }
        public bool IsVerified { get; set; }
        public bool VerificationSuccess { get; set; }
    }
}
