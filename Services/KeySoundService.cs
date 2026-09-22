using System.IO;
using System.Media;
using System.Text;

namespace Klucznik.Services;

public static class KeySoundService
{
    // Własny WAV, niezależny od ustawień dźwięków zdarzeń Windows.
    // Odtwarzanie nie blokuje obsługi czytnika i nie wpływa na zapis operacji.
    public static void Play(bool denied = false)
    {
        _ = Task.Run(() =>
        {
            try
            {
                const int rate = 22050;
                int count = denied ? 3 : 2;
                int toneSamples = rate / 5;
                int gapSamples = rate / 10;
                int samples = count * (toneSamples + gapSamples);
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(36 + samples * 2);
                    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);
                    writer.Write((short)1);
                    writer.Write((short)1);
                    writer.Write(rate);
                    writer.Write(rate * 2);
                    writer.Write((short)2);
                    writer.Write((short)16);
                    writer.Write(Encoding.ASCII.GetBytes("data"));
                    writer.Write(samples * 2);
                    for (int i = 0; i < samples; i++)
                    {
                        int position = i % (toneSamples + gapSamples);
                        double envelope = position >= toneSamples ? 0 :
                            Math.Min(1, Math.Min(position / 200.0, (toneSamples - position) / 200.0));
                        writer.Write((short)(12000 * envelope * Math.Sin(2 * Math.PI *
                            (denied ? 350 : 1000) * position / rate)));
                    }
                }
                stream.Position = 0;
                using var player = new SoundPlayer(stream);
                player.PlaySync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Nie można odtworzyć dźwięku: {ex.Message}");
            }
        });
    }
}
