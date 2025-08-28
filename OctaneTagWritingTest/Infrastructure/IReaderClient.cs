using Impinj.OctaneSdk;
using Org.LLRP.LTK.LLRPV1;
using Org.LLRP.LTK.LLRPV1.Impinj;

namespace OctaneTagWritingTest.Infrastructure
{
    /// <summary>
    /// Abstraction for Impinj reader interactions enabling test mocking.
    /// </summary>
    public interface IReaderClient
    {
        event TagsReportedEventHandler TagsReported;
        event ImpinjReader.TagOpCompleteHandler TagOpComplete;
        event GpiEventHandler GpiChanged;

        void Connect(string hostname);
        void ApplyDefaultSettings();
        Settings QueryDefaultSettings();
        MSG_ADD_ROSPEC BuildAddROSpecMessage(Settings settings);
        MSG_SET_READER_CONFIG BuildSetReaderConfigMessage(Settings settings);
        void ApplySettings(Settings settings);
        void ApplySettings(MSG_SET_READER_CONFIG setConfig, MSG_ADD_ROSPEC addRospec);
        void Start();
        void Stop();
        void Disconnect();
        void AddOpSequence(TagOpSequence sequence);
        void DeleteAllOpSequences();
        bool IsConnected { get; }
    }
}
