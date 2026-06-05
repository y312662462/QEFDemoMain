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

        /// <summary>
        /// Encodes interleaved float PCM samples (range -1..1) into a 16-bit PCM RIFF/WAVE
        /// byte array. The header is written from the ACTUAL <paramref name="sampleRate"/>
        /// and <paramref name="channels"/> supplied by the caller (e.g. the recorded
        /// <c>AudioClip.frequency</c> / <c>AudioClip.channels</c>), never a hard-coded rate.
        /// Returns null on invalid input.
        /// </summary>
        public static byte[] EncodeToWav16(float[] samples, int channels, int sampleRate)
        {
            if (samples == null || samples.Length == 0)
            {
                Debug.LogError("[WavUtility] EncodeToWav16: no samples to encode.");
                return null;
            }

            if (channels < 1 || sampleRate <= 0)
            {
                Debug.LogError($"[WavUtility] EncodeToWav16: invalid channels={channels} or sampleRate={sampleRate}.");
                return null;
            }

            const int bitsPerSample = 16;
            int bytesPerSample = bitsPerSample / 8;
            int dataLength = samples.Length * bytesPerSample;
            int blockAlign = channels * bytesPerSample;
            int byteRate = sampleRate * blockAlign;

            // 44-byte canonical header + PCM body.
            var buffer = new byte[44 + dataLength];

            WriteId(buffer, 0, "RIFF");
            BitConverter.GetBytes(36 + dataLength).CopyTo(buffer, 4); // ChunkSize
            WriteId(buffer, 8, "WAVE");

            WriteId(buffer, 12, "fmt ");
            BitConverter.GetBytes(16).CopyTo(buffer, 16);            // Subchunk1Size (PCM)
            BitConverter.GetBytes((short)1).CopyTo(buffer, 20);     // AudioFormat = PCM
            BitConverter.GetBytes((short)channels).CopyTo(buffer, 22);
            BitConverter.GetBytes(sampleRate).CopyTo(buffer, 24);
            BitConverter.GetBytes(byteRate).CopyTo(buffer, 28);
            BitConverter.GetBytes((short)blockAlign).CopyTo(buffer, 32);
            BitConverter.GetBytes((short)bitsPerSample).CopyTo(buffer, 34);

            WriteId(buffer, 36, "data");
            BitConverter.GetBytes(dataLength).CopyTo(buffer, 40);

            int pos = 44;
            for (int i = 0; i < samples.Length; i++)
            {
                float clamped = samples[i];
                if (clamped > 1f)
                {
                    clamped = 1f;
                }
                else if (clamped < -1f)
                {
                    clamped = -1f;
                }

                short s = (short)Mathf.RoundToInt(clamped * 32767f);
                buffer[pos] = (byte)(s & 0xFF);
                buffer[pos + 1] = (byte)((s >> 8) & 0xFF);
                pos += 2;
            }

            return buffer;
        }

        private static void WriteId(byte[] buffer, int offset, string id)
        {
            for (int i = 0; i < id.Length; i++)
            {
                buffer[offset + i] = (byte)id[i];
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
