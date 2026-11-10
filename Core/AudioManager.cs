using OpenTK.Audio.OpenAL;
using NAudio.Wave; // Добавьте в начало файла AudioManager.cs

namespace GeometryJump.Core;

internal static class AudioManager
{
    private static ALDevice _device;
    private static ALContext _context;
    private static readonly List<int> _activeSources = new();
    private static int _musicSource;
    private static int _musicBuffer;
    private static readonly HttpClient _httpClient = new HttpClient();
    public static bool IsDownloading { get; private set; } = false;
    public static float DownloadProgress { get; private set; } = 0f;
    public static void Initialize()
    {
        _device = ALC.OpenDevice(null);
        _context = ALC.CreateContext(_device, Array.Empty<int>());
        ALC.MakeContextCurrent(_context);

        // Источник для фоновой музыки
        _musicSource = AL.GenSource();
        AL.Source(_musicSource, ALSourceb.Looping, true);
    }

    // Воспроизведение коротких звуков (SFX) в фоне
    public static void PlaySound(byte[] rawWavData)
    {
        int buffer = AL.GenBuffer();
        int source = AL.GenSource();

        // Парсинг простейшего WAV заголовка вручную для примера
        // В продакшене лучше использовать полноценный декодер
        ParseWav(rawWavData, out ALFormat format, out byte[] audioData, out int sampleRate);

        AL.BufferData(buffer, format, audioData, sampleRate);
        AL.Source(source, ALSourcei.Buffer, buffer);
        AL.SourcePlay(source);

        _activeSources.Add(source);

        // Очистка отыгравших источников
        CleanupSources();
    }
    private static byte[] _currentPlayingWavBytes = Array.Empty<byte>();

    // Модифицируйте метод PlayMusic, сохранив массив байт:
    public static void PlayMusic(byte[] rawWavData)
    {
        _currentPlayingWavBytes = rawWavData; // Сохраняем для анализа громкости
        AL.SourceStop(_musicSource);
        if (AL.IsBuffer(_musicBuffer)) AL.DeleteBuffer(_musicBuffer);
        _musicBuffer = AL.GenBuffer();
        ParseWav(rawWavData, out ALFormat format, out byte[] audioData, out int sampleRate);
        AL.BufferData(_musicBuffer, format, audioData, sampleRate);
        AL.Source(_musicSource, ALSourcei.Buffer, _musicBuffer);
        AL.SourcePlay(_musicSource);
    }

    // Получение мгновенной RMS амплитуды (от 0.0 до 1.0)
    public static float GetCurrentMusicAmplitude()
    {
        if (_currentPlayingWavBytes == null || _currentPlayingWavBytes.Length < 48) return 0f;

        float sec = GetMusicPlaybackPosition();
        int sampleRate = BitConverter.ToInt32(_currentPlayingWavBytes, 24);
        short channels = BitConverter.ToInt16(_currentPlayingWavBytes, 22);
        short bitsPerSample = BitConverter.ToInt16(_currentPlayingWavBytes, 34);

        int bytesPerSample = (bitsPerSample / 8) * channels;
        int currentSampleOffset = 44 + (int)(sec * sampleRate * bytesPerSample);

        if (currentSampleOffset >= _currentPlayingWavBytes.Length - 512) return 0f;

        // Считаем среднее квадратичное значение небольшого окна сэмплов
        float sum = 0f;
        int sampleCount = 128;

        for (int i = 0; i < sampleCount; i++)
        {
            int opt = currentSampleOffset + (i * bytesPerSample);
            if (opt >= _currentPlayingWavBytes.Length - 2) break;

            short sampleVal = BitConverter.ToInt16(_currentPlayingWavBytes, opt);
            float normSample = sampleVal / 32768f;
            sum += normSample * normSample;
        }

        float rms = MathF.Sqrt(sum / sampleCount);
        return Math.Clamp(rms * 2.0f, 0f, 1f); // Умножаем на коэфф. чувствительности
    }


    public static async Task<bool> DownloadSongFromNewgroundsAsync(int songId)
    {
        if (songId <= 0) return false;

        string directory = "Assets/Songs";
        string localPathWav = Path.Combine(directory, $"{songId}.wav");
        string tempMp3Path = Path.Combine(directory, $"{songId}_temp.mp3");

        if (File.Exists(localPathWav)) return true; // Уже скачано и конвертировано

        Directory.CreateDirectory(directory);
        IsDownloading = true;
        DownloadProgress = 0f;

        try
        {
            string url = $"https://newgrounds.com/audio/download/{songId}";

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;
            using var stream = await response.Content.ReadAsStreamAsync();
            using (var fileStream = new FileStream(tempMp3Path, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                byte[] buffer = new byte[8192];
                long totalReadBytes = 0;
                int readBytes;

                while ((readBytes = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, readBytes);
                    totalReadBytes += readBytes;

                    if (totalBytes.HasValue)
                    {
                        DownloadProgress = (float)totalReadBytes / totalBytes.Value * 0.9f; // 90% на скачивание
                    }
                }
            }

            // --- Конвертация MP3 в WAV для OpenAL ---
            await Task.Run(() =>
            {
                using (var mp3Reader = new Mp3FileReader(tempMp3Path))
                {
                    // Приводим к стандартному PCM формату, который ждет ParseWav
                    WaveFileWriter.CreateWaveFile(localPathWav, mp3Reader);
                }

                // Удаляем временный MP3 файл, оставляя только готовый WAV
                if (File.Exists(tempMp3Path)) File.Delete(tempMp3Path);
            });

            DownloadProgress = 1.0f;
            IsDownloading = false;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке трека Newgrounds (ID: {songId}): {ex.Message}");
            IsDownloading = false;

            // Очистка мусора в случае сбоя
            if (File.Exists(tempMp3Path)) File.Delete(tempMp3Path);
            if (File.Exists(localPathWav)) File.Delete(localPathWav);

            return false;
        }
    }


    public static void PlayNewgroundsSong(int songId)
    {
        if (songId <= 0) return;

        string localPathWav = $"Assets/Songs/{songId}.wav";
        string localPathMp3 = $"Assets/Songs/{songId}.mp3";
        string activePath = File.Exists(localPathWav) ? localPathWav : (File.Exists(localPathMp3) ? localPathMp3 : null);

        if (activePath == null) return;

        try
        {
            byte[] songData = File.ReadAllBytes(activePath);
            PlayMusic(songData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка воспроизведения аудио: {ex.Message}");
        }
    }

    public static void SetMusicPlaybackPosition(float seconds)
    {
        AL.Source(_musicSource, ALSourcef.SecOffset, seconds);
    }

    public static float GetMusicPlaybackPosition()
    {
        AL.GetSource(_musicSource, ALSourcef.SecOffset, out float seconds);
        return seconds;
    }

    private static void CleanupSources()
    {
        for (int i = _activeSources.Count - 1; i >= 0; i--)
        {
            int source = _activeSources[i];
            AL.GetSource(source, ALGetSourcei.SourceState, out int state);
            if (state == (int)ALSourceState.Stopped)
            {
                AL.DeleteSource(source);
                _activeSources.RemoveAt(i);
            }
        }
    }

    private static void ParseWav(byte[] wavBytes, out ALFormat format, out byte[] data, out int sampleRate)
    {
        // Быстрый парсер заголовка RIFF-WAVE
        short channels = BitConverter.ToInt16(wavBytes, 22);
        sampleRate = BitConverter.ToInt32(wavBytes, 24);
        short bitsPerSample = BitConverter.ToInt16(wavBytes, 34);

        format = channels == 1
            ? (bitsPerSample == 8 ? ALFormat.Mono8 : ALFormat.Mono16)
            : (bitsPerSample == 8 ? ALFormat.Stereo8 : ALFormat.Stereo16);

        int dataLength = wavBytes.Length - 44;
        data = new byte[dataLength];
        Array.Copy(wavBytes, 44, data, 0, dataLength);
    }
    public static void StopMusic()
    {
        AL.SourceStop(_musicSource);
    }

    public static void Shutdown()
    {
        AL.SourceStop(_musicSource);
        AL.DeleteSource(_musicSource);
        if (AL.IsBuffer(_musicBuffer)) AL.DeleteBuffer(_musicBuffer);

        ALC.MakeContextCurrent(ALContext.Null);
        ALC.DestroyContext(_context);
        ALC.CloseDevice(_device);
    }
}
