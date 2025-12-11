using OpenAL;
using OpenAL.managed;
using System.Diagnostics;

namespace reverbpan;

public class PCMData
{
    public int format;
    public short[] shortData;
    public int byteCount;
    public int sampleRate;
    public int duration;
}

internal class Program
{
    static void Main(string[] args)
    {
        // Create a device and context
        var deviceNames = AL.GetStringList(IntPtr.Zero, AL.ALC_ALL_DEVICES_SPECIFIER);
        var device = new ALDevice(deviceNames[0]);
        var context = new ALContext(device, new ALContextSettings()
        {
            HRTFEnabled = true,
            SampleRate = 48000,
            MaximumAuxiliarySends = 1
        });
        
        context.MakeCurrent();
        context.Process();


        // Generate a buffer and source
        uint bufferID = AL.GenBuffer();
        uint sourceID = AL.GenSource();

        var data = LoadOgg("gunfire.ogg");

        AL.BufferData(bufferID, data.format, data.shortData, data.byteCount, data.sampleRate);
        AL.Sourcei(sourceID, AL.AL_BUFFER, (int)bufferID);


        // Create a reverb effect
        uint effectID = AL.GenEffect();
        AL.Effecti(effectID, AL.AL_EFFECT_TYPE, AL.AL_EFFECT_EAXREVERB);


        // Customise the reverb effect
        {
            // Clear reflection
            AL.Effectf(effectID, AL.AL_EAXREVERB_DENSITY, 0.0f);
            AL.Effectf(effectID, AL.AL_EAXREVERB_DIFFUSION, 0.0f);

            // Maximum gain
            AL.Effectf(effectID, AL.AL_EAXREVERB_GAIN, 1);
            AL.Effectf(effectID, AL.AL_EAXREVERB_GAINHF, 1);
            AL.Effectf(effectID, AL.AL_EAXREVERB_GAINLF, 1.0f);

            // Short decay
            AL.Effectf(effectID, AL.AL_EAXREVERB_DECAY_TIME, 0.1f);
            AL.Effectf(effectID, AL.AL_EAXREVERB_DECAY_HFRATIO, 1.0f);
            AL.Effectf(effectID, AL.AL_EAXREVERB_DECAY_LFRATIO, 1.0f);

            // Strong reflection after 300m
            AL.Effectf(effectID, AL.AL_EAXREVERB_REFLECTIONS_GAIN, 3.16f);
            AL.Effectf(effectID, AL.AL_EAXREVERB_REFLECTIONS_DELAY, 0.3f);
            AL.Effectfv(effectID, AL.AL_EAXREVERB_REFLECTIONS_PAN, [0.0f, 0.0f, 0.0f]);

            // No late reverb to keep the reflection clear
            AL.Effectf(effectID, AL.AL_EAXREVERB_LATE_REVERB_GAIN, 0.0f);
            AL.Effectf(effectID, AL.AL_EAXREVERB_LATE_REVERB_DELAY, 0.0f);
            AL.Effectfv(effectID, AL.AL_EAXREVERB_LATE_REVERB_PAN, [0.0f, 0.0f, 0.0f]);

            // Disable echo effects
            AL.Effectf(effectID, AL.AL_EAXREVERB_ECHO_TIME, 0.075f);
            AL.Effectf(effectID, AL.AL_EAXREVERB_ECHO_DEPTH, 0.0f);

            // Disable modulation
            AL.Effectf(effectID, AL.AL_EAXREVERB_MODULATION_TIME, 0.04f);
            AL.Effectf(effectID, AL.AL_EAXREVERB_MODULATION_DEPTH, 0.0f);

            // Air absorption and references
            AL.Effectf(effectID, AL.AL_EAXREVERB_AIR_ABSORPTION_GAINHF, 0.994f);
            AL.Effectf(effectID, AL.AL_EAXREVERB_HFREFERENCE, 4000.0f);
            AL.Effectf(effectID, AL.AL_EAXREVERB_LFREFERENCE, 300.0f);
            AL.Effectf(effectID, AL.AL_EAXREVERB_ROOM_ROLLOFF_FACTOR, 0.0f);
            AL.Effecti(effectID, AL.AL_EAXREVERB_DECAY_HFLIMIT, 1);
        }


        // Create an effect slot and apply the effect
        uint effectSlotID = AL.GenAuxiliaryEffectSlot();
        AL.AuxiliaryEffectSloti(effectSlotID, AL.AL_EFFECTSLOT_EFFECT, (int)effectID);


        // Create a filter to silence the dry path
        uint filterID = AL.GenFilter();
        AL.Filteri(filterID, AL.AL_FILTER_TYPE, AL.AL_FILTER_LOWPASS);
        AL.Filterf(filterID, AL.AL_LOWPASS_GAIN, 0.0f);


        // Set up the listener
        AL.Listenerfv(AL.AL_POSITION, [ 0, 0, 0]);
        AL.Listenerfv(AL.AL_VELOCITY, [0, 0, 0]);
        AL.Listenerfv(AL.AL_ORIENTATION, [0, 0, -1, 0, 1, 0]);


        // Helper function
        void PlayWithPan(float x, float y, float z)
        {
            Console.WriteLine($"Playing with reverb pan ({x}, {y}, {z})");

            AL.Effectfv(effectID, AL.AL_EAXREVERB_REFLECTIONS_PAN, [x, y, z]);
            AL.Effectfv(effectID, AL.AL_EAXREVERB_LATE_REVERB_PAN, [x, y, z]);

            AL.AuxiliaryEffectSloti(effectSlotID, AL.AL_EFFECTSLOT_EFFECT, (int)effectID);

            // Play the source
            AL.Sourcei(sourceID, AL.AL_DIRECT_FILTER, (int)filterID);
            AL.Source3i(sourceID, AL.AL_AUXILIARY_SEND_FILTER, (int)effectSlotID, 0, 0);
            AL.SourcePlay(sourceID);
        }


        // Play the sound with different pan directions
        Thread.Sleep(500);

        PlayWithPan(0, 0, 0);
        Thread.Sleep(2000);

        PlayWithPan(1, 0, 0);
        Thread.Sleep(2000);

        PlayWithPan(-1, 0, 0);
        Thread.Sleep(2000);

        PlayWithPan(0, 0, 1);
        Thread.Sleep(2000);

        PlayWithPan(0, 0, -1);
        Thread.Sleep(2000);


        // Sanity check
        var alError = AL.GetError();
        var alcError = AL.GetError(device.handle);

        Debug.Assert(alError == 0);
        Debug.Assert(alcError == 0);


        // Shut down
        Console.WriteLine("Shutting down");

        context.Destroy();
        device.Close();
    }

    static PCMData LoadOgg(string path)
    {
        using var stm = File.OpenRead(path);
        using var vorbis = new NVorbis.VorbisReader(stm);
        

        // Get the channels & sample rate
        var channels = vorbis.Channels;
        var sampleRate = vorbis.SampleRate;
        int bitDepth = 16;

        long totalSamplesLong = vorbis.TotalSamples * vorbis.Channels;

        // Overflow check: sound is too long to load
        if (totalSamplesLong > int.MaxValue)
        {
            Debug.Assert(false);
            return null;
        }


        // Convert OGG data to PCM data
        var totalSamples = (int)totalSamplesLong;
        float[] readBuffer = new float[totalSamples];

        vorbis.ReadSamples(readBuffer, 0, totalSamples);


        // Convert float data to short data
        var shortData = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
            shortData[i] = (short)(readBuffer[i] * short.MaxValue);


        // Return all sound data
        var format = GetSoundFormat(channels, bitDepth);
        var duration = (int)vorbis.TotalTime.TotalMilliseconds;

        return new PCMData()
        {
            format = format,
            shortData = shortData,
            byteCount = totalSamples * 2,
            sampleRate = sampleRate,
            duration = duration,
        };        
    }

    static int GetSoundFormat(int channels, int bitDepth)
    {
        if (channels == 1 && bitDepth == 8)
            return AL.AL_FORMAT_MONO8;
        if (channels == 1 && bitDepth == 16)
            return AL.AL_FORMAT_MONO16;
        else if (channels == 2 && bitDepth == 8)
            return AL.AL_FORMAT_STEREO8;
        else if (channels == 2 && bitDepth == 16)
            return AL.AL_FORMAT_STEREO16;

        // Other formats not supported
        Debug.Assert(false);
        return AL.AL_FORMAT_MONO8;
    }
}