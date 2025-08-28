using Impinj.OctaneSdk;
using Org.LLRP.LTK.LLRPV1;
using Org.LLRP.LTK.LLRPV1.Impinj;

namespace OctaneTagWritingTest.Infrastructure
{
    /// <summary>
    /// Default implementation of <see cref="IReaderClient"/> that wraps an <see cref="ImpinjReader"/>.
    /// </summary>
    public class ImpinjReaderClient : IReaderClient
    {
        private readonly ImpinjReader _reader = new();

        public event TagsReportedEventHandler TagsReported
        {
            add { _reader.TagsReported += value; }
            remove { _reader.TagsReported -= value; }
        }

        public event TagOpCompleteEventHandler TagOpComplete
        {
            add { _reader.TagOpComplete += value; }
            remove { _reader.TagOpComplete -= value; }
        }

        public event GpiEventHandler GpiChanged
        {
            add { _reader.GpiChanged += value; }
            remove { _reader.GpiChanged -= value; }
        }

        public void Connect(string hostname) => _reader.Connect(hostname);

        public void ApplyDefaultSettings() => _reader.ApplyDefaultSettings();

        public Settings QueryDefaultSettings() => _reader.QueryDefaultSettings();

        public MSG_ADD_ROSPEC BuildAddROSpecMessage(Settings settings) => _reader.BuildAddROSpecMessage(settings);

        public MSG_SET_READER_CONFIG BuildSetReaderConfigMessage(Settings settings) => _reader.BuildSetReaderConfigMessage(settings);

        public void ApplySettings(Settings settings) => _reader.ApplySettings(settings);

        public void ApplySettings(MSG_SET_READER_CONFIG setConfig, MSG_ADD_ROSPEC addRospec) => _reader.ApplySettings(setConfig, addRospec);

        public void Start() => _reader.Start();

        public void Stop() => _reader.Stop();

        public void Disconnect() => _reader.Disconnect();

        public void AddOpSequence(TagOpSequence sequence) => _reader.AddOpSequence(sequence);

        public void DeleteAllOpSequences() => _reader.DeleteAllOpSequences();

        public bool IsConnected => _reader.IsConnected;
    }
}
