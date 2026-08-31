namespace Aigf.Companion.Voice
{
    public interface IVad
    {
        bool ContainsSpeech(float[] samples, int sampleCount, int sampleRate);
    }
}
