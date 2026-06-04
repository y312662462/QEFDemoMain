Place a short English speech WAV file named "test.wav" in this folder to test STT.

Recommended format for best Azure STT compatibility:
- WAV (RIFF), PCM 16-bit, mono, 16000 Hz

The ExternalServiceTester reads StreamingAssets/TestAudio/test.wav, sends the raw
bytes to the configured STT provider, and logs the transcript to the Console.

Do not commit large or copyrighted audio. This file is only a placeholder/instructions.
