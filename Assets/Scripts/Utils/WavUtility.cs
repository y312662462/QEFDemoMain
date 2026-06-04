using System;
using UnityEngine;

namespace MultiAgentNPC.Utils
{
    /// <summary>
    /// Minimal RIFF/WAV decoder: converts WAV bytes (PCM 16-bit, or IEEE float 32-bit)
    /// into a Unity <see cref="AudioClip"/>. Used to play TTS audio returned as WAV.
    /// Must be called on the main thread (AudioClip.Create requirement).
    /// </summary>
    public static class WavUtility
    {
        /// <summary>
        /// Parses <paramref name="wavBytes"/> into an AudioClip. Returns null and logs
        /// an error on malformed/unsupported input.
        /// </summary>
        public static AudioClip ToAudioClip(byte[] wavBytes, string clipName = "TTSClip")
        {
            if (wavBytes == null || wavBytes.Length < 44)
            {
                Debug.LogError("[WavUtility] WAV data is null or too short.");
                return null;
            }

            try
            {
                if (!Matches(wavBytes, 0, "RIFF") || !Matches(wavBytes, 8, "WAVE"))
                {
                    Debug.LogError("[WavUtility] Not a RIFF/WAVE file.");
                    return null;
                }

                int channels = 1;
                int sampleRate = 0;
                int bitsPerSample = 16;
                int audioFormat = 1; // 1 = PCM, 3 = IEEE float
                int dataOffset = -1;
                int dataLength = 0;

                // Walk chunks starting after "RIFF"<size>"WAVE" (offset 12).
                int pos = 12;
                while (pos + 8 <= wavBytes.Length)
                {
                    string chunkId = ReadId(wavBytes, pos);
                    int chunkSize = BitConverter.ToInt32(wavBytes, pos + 4);
                    int chunkBody = pos + 8;

                    if (chunkId == "fmt ")
                    {
                        audioFormat = BitConverter.ToInt16(wavBytes, chunkBody);
                        channels = BitConverter.ToInt16(wavBytes, chunkBody + 2);
                        sampleRate = BitConverter.ToInt32(wavBytes, chunkBody + 4);
                        bitsPerSample = BitConverter.ToInt16(wavBytes, chunkBody + 14);
                    }
                    else if (chunkId == "data")
                    {
                        dataOffset = chunkBody;
                        dataLength = chunkSize;
                    }

                    // Chunks are word-aligned (even size).
                    int advance = chunkSize + (chunkSize % 2);
                    pos = chunkBody + advance;
                }

                if (sampleRate <= 0 || dataOffset < 0 || dataLength <= 0)
                {
                    Debug.LogError("[WavUtility] Missing fmt/data chunk or invalid sample rate.");
                    return null;
                }

                // Clamp data length to actual buffer.
                dataLength = Mathf.Min(dataLength, wavBytes.Length - dataOffset);

                float[] samples = DecodeSamples(wavBytes, dataOffset, dataLength, bitsPerSample, audioFormat);
                if (samples == null)
                {
                    return null;
                }

                int sampleCount = samples.Length / Mathf.Max(1, channels);
                AudioClip clip = AudioClip.Create(clipName, sampleCount, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
            catch (Exception e)
            {
                Debug.LogError($"[WavUtility] Failed to decode WAV: {e.Message}");
                return null;
            }
        }

        private static float[] DecodeSamples(byte[] data, int offset, int length, int bitsPerSample, int audioFormat)
        {
            if (audioFormat == 3 && bitsPerSample == 32)
            {
                int count = length / 4;
                var samples = new float[count];
                for (int i = 0; i < count; i++)
                {
                    samples[i] = BitConverter.ToSingle(data, offset + i * 4);
                }

                return samples;
            }

            if (audioFormat == 1)
            {
                switch (bitsPerSample)
                {
                    case 16:
                    {
                        int count = length / 2;
                        var samples = new float[count];
                        for (int i = 0; i < count; i++)
                        {
                            short s = BitConverter.ToInt16(data, offset + i * 2);
                            samples[i] = s / 32768f;
                        }

                        return samples;
                    }
                    case 8:
                    {
                        var samples = new float[length];
                        for (int i = 0; i < length; i++)
                        {
                            // 8-bit WAV is unsigned (0..255).
                            samples[i] = (data[offset + i] - 128) / 128f;
                        }

                        return samples;
                    }
                    case 24:
                    {
                        int count = length / 3;
                        var samples = new float[count];
                        for (int i = 0; i < count; i++)
                        {
                            int b0 = data[offset + i * 3];
                            int b1 = data[offset + i * 3 + 1];
                            int b2 = data[offset + i * 3 + 2];
                            int sample = (b2 << 16) | (b1 << 8) | b0;
                            if ((sample & 0x800000) != 0)
                            {
                                sample |= unchecked((int)0xFF000000);
                            }

                            samples[i] = sample / 8388608f;
                        }

                        return samples;
                    }
                }
            }

            Debug.LogError($"[WavUtility] Unsupported WAV format (audioFormat={audioFormat}, bits={bitsPerSample}).");
            return null;
        }

        private static bool Matches(byte[] data, int offset, string id)
        {
            return ReadId(data, offset) == id;
        }

        private static string ReadId(byte[] data, int offset)
        {
            if (offset + 4 > data.Length)
            {
                return string.Empty;
            }

            return new string(new[]
            {
                (char)data[offset],
                (char)data[offset + 1],
                (char)data[offset + 2],
                (char)data[offset + 3]
            });
        }
    }
}
