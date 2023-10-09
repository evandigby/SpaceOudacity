// See https://aka.ms/new-console-template for more information
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenCvSharp;
using OpenCvSharp.Features2D;

const int maxFrames = 30 * 30;
const int maxHue = 359;
double minHueWavelength = HueToWavelength(0);
double minMaxHueWaveLengthDelta = HueToWavelength(maxHue) - HueToWavelength(0);

const int sampleRate = 44100;
const int signalChannels = 2;
var oneFrame = TimeSpan.FromSeconds((double)1 / 30);

var signalWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, signalChannels);
var outputWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);

using var orb = ORB.Create(500);

var p = new SimpleBlobDetector.Params
{
    FilterByColor = true,
    BlobColor = 255,
    MinArea = 3,
};


using var sift = SIFT.Create(100);

using var bf = new BFMatcher(NormTypes.Hamming);

using var sbd = SimpleBlobDetector.Create(p);

var matchObjects = new List<MatchObject>();

var secondToFirst = new Dictionary<int, int>();

using (var capture = VideoCapture.FromFile(@"E:\SpaceApps\NebulaVideo.mov"))
{
    var descriptorsLast = new Mat();
    KeyPoint[]? keypointsLast = null;

    using var output = new VideoWriter(@"E:\SpaceApps\NebulaVideoOutputDetection.avi", FourCC.WMV1, capture.Fps, new Size(capture.FrameWidth, capture.FrameHeight), true);

    for (int i = 0; i < maxFrames; i++)
    {
        using var blur = new Mat();
        using var outImg = new Mat();
        using var thresh = new Mat();

        var currentFrame = capture.PosFrames;
        using var img = capture.RetrieveMat();
        if (img.Empty()) break;

        Cv2.ExtractChannel(img, thresh, 0);

        var keypoints = sbd.Detect(thresh);

        var descriptors = new Mat();
        orb.Compute(thresh, ref keypoints, descriptors);
        Cv2.DrawKeypoints(thresh, keypoints, outImg, Scalar.Red, DrawMatchesFlags.DrawRichKeypoints);
        var newSecondToFirst = new Dictionary<int, int>();

        if (i > 0 && keypointsLast != null)
        {
            var matches = bf.KnnMatch(descriptorsLast, descriptors, 1).Select(m => m[0]).OrderByDescending(m => m.Distance).ToArray();

            var frame1 = i - 1;
            var frame2 = i;

            foreach (var match in matches)
            {
                MatchObject matchObject;

                if (secondToFirst.TryGetValue(match.QueryIdx, out var matchObjectIndex))
                {
                    matchObject = matchObjects[matchObjectIndex];

                    if (matchObject.Index[frame1] != match.QueryIdx)
                    {
                        throw new Exception("Algorithm wrong");
                    }
                }
                else
                {
                    matchObject = new MatchObject(maxFrames);
                    matchObject.Index[frame1] = match.QueryIdx;
                    matchObject.KeyPoints[frame1] = keypointsLast[match.QueryIdx];

                    matchObjectIndex = matchObjects.Count;
                    matchObjects.Add(matchObject);
                }

                matchObject.Matches[frame2] = match;
                matchObject.Index[frame2] = match.TrainIdx;
                matchObject.KeyPoints[frame2] = keypoints[match.TrainIdx];
                newSecondToFirst[match.TrainIdx] = matchObjectIndex;
            }

            output.Write(outImg);
        }

        secondToFirst = newSecondToFirst;
        descriptorsLast?.Dispose();
        descriptorsLast = descriptors;
        keypointsLast = keypoints;
        Console.WriteLine($"Detected frame {i}");
    }
}

var bestMatchObjects = matchObjects.OrderByDescending(mo => mo.KeyPoints.Count(kp => kp is not null)).Take(20).ToArray();

var test = matchObjects
    .OrderByDescending(mo => mo.KeyPoints.Count(kp => kp is not null))
    .Select(i => new { Count = i.KeyPoints.Count(kp => kp is not null), i })
    .Where(i => i.Count > 100)
    .ToArray();

var good = new List<MatchObject>();

foreach (var obj in test)
{
    var longest = 0;
    var current = 0;
    foreach (var kp in obj.i.KeyPoints)
    {
        if (kp == null)
        {
            if (current > longest)
            {
                longest = current;
            }
            current = 0;
        }
        else
        {
            current++;
        }
    }

    if (current > longest)
    {
        longest = current;
    }

    if (longest >= 100)
    {
        good.Add(obj.i);
    }
}


var totalFrames = 0;
using (var capture = VideoCapture.FromFile(@"E:\SpaceApps\NebulaVideo.mov"))
{
    using var output = new VideoWriter(@"E:\SpaceApps\NebulaVideoOutputMatched.avi", FourCC.WMV1, capture.Fps, new Size(capture.FrameWidth, capture.FrameHeight), true);
    for (int i = 0; i < maxFrames; i++, totalFrames++)
    {
        var currentFrame = capture.PosFrames;
        using var img = capture.RetrieveMat();
        if (img.Empty()) break;

        using var outImg = new Mat();

        var matchedWithNotNull = bestMatchObjects.Where(o => o.KeyPoints[i] is not null).ToArray();

        var matchedKeyPoints = new List<KeyPoint>();

        foreach (var obj in bestMatchObjects)
        {
            var kp = obj.KeyPoints[i];
            if (kp != null)
            {
                var bgr = img.At<Vec3b>((int)kp.Value.Pt.Y, (int)kp.Value.Pt.X);
                obj.Vecs[i] = bgr;
                matchedKeyPoints.Add(kp.Value);
            }
        }

        if (matchedKeyPoints.Count > 0)
        {
            Cv2.DrawKeypoints(img, matchedKeyPoints, outImg, Scalar.Purple, DrawMatchesFlags.DrawRichKeypoints);
            output.Write(outImg);
        }
        else
        {
            output.Write(img);
        }
        Console.WriteLine($"Wrote frame {i}");
    }
}

foreach (var obj in bestMatchObjects)
{
    for (int i = 0; i < totalFrames;)
    {
        var bgr = obj.Vecs[i];
        if (bgr == null)
        {
            var streak = obj.Vecs.Skip(i).TakeWhile(v => v is null).ToList();

            obj.SampleProviders.Add(new SilenceProvider(signalWaveFormat).ToSampleProvider().Take(oneFrame * streak.Count));
            i += streak.Count;
        }
        else
        {
            var streak = obj.Vecs.Skip(i).TakeWhile(v => v is not null).ToList();

            var sig = ColorToSignal(bgr.Value, 0.2);

            obj.SampleProviders.Add(sig.Take(oneFrame * streak.Count));
            i += streak.Count;
        }
    }
}

var mixer = new MixingSampleProvider(outputWaveFormat);

foreach (var obj in bestMatchObjects)
{
    var samples = new ConcatenatingSampleProvider(obj.SampleProviders);
    mixer.AddMixerInput(samples);
}

var totalFramesTime = oneFrame * totalFrames;

WaveFileWriter.CreateWaveFile(@"E:\SpaceApps\test.wav", mixer.Take(totalFramesTime).ToWaveProvider());

ISampleProvider ColorToSignal(Vec3b color, double gain)
{
    var (h, _, _) = RGBToHSV(color);
    var wave = HueToWavelength(h); ;
    return LightWaveToSignal(wave, gain);
}

ISampleProvider LightWaveToSignal(double wavelength, double gain)
{
    var minimized = wavelength - minHueWavelength;

    var waveRatio = minimized / minMaxHueWaveLengthDelta;

    const double pianoMinFrequency = 27.5d;
    const double pianoMaxFrequency = 1000d;//186.009d;
    const double pianoDelta = pianoMaxFrequency - pianoMinFrequency;

    var frequency = (waveRatio * pianoDelta) - pianoMinFrequency;

    return new SignalGenerator(sampleRate, signalChannels)
    {
        Gain = gain,
        Frequency = frequency,
        Type = SignalGeneratorType.Sin,
    };
}

static double HueToWavelength(double hue) => 650d - (250d / maxHue * hue);

static (double, double, double) RGBToHSV(Vec3b rgb)
{
    var (b, g, r) = rgb;

    double delta, min;
    double h = 0, s, v;

    min = Math.Min(Math.Min(r, g), b);
    v = Math.Max(Math.Max(r, g), b);
    delta = v - min;

    if (v == 0.0)
        s = 0;
    else
        s = delta / v;

    if (s == 0)
        h = 0.0;

    else
    {
        if (r == v)
            h = (g - b) / delta;
        else if (g == v)
            h = 2 + ((b - r) / delta);
        else if (b == v)
            h = 4 + ((r - g) / delta);

        h *= 60;

        if (h < 0.0)
            h = h + maxHue;
    }

    return (h, s, v / 255);
}

internal record MatFrame(int Frame, Mat Mat);

internal record MatFrameBlobs(MatFrame Mat, Mat Greyscale, Mat Descriptors, IReadOnlyList<KeyPoint> KeyPoints);

internal record MatFrameBlobsMatches(MatFrameBlobs F1, MatFrameBlobs F2, DMatch[] Matches);

internal record MatFrameBlobsImage(MatFrameBlobs Mat, Mat Output);

internal record MatchKeyPoints(int FrameOne, int FrameTwo, IReadOnlyList<KeyPoint> F1, IReadOnlyList<KeyPoint> F2, DMatch[] Matches);

internal class MatchObject
{
    public MatchObject(int numFrames)
    {
        Index = Enumerable.Range(0, numFrames).Select(_ => -1).ToArray();
        KeyPoints = new KeyPoint?[numFrames];
        Matches = new DMatch?[numFrames];
        Vecs = new Vec3b?[numFrames];
    }

    public int[] Index { get; }
    public KeyPoint?[] KeyPoints { get; }
    public DMatch?[] Matches { get; }
    public List<ISampleProvider> SampleProviders { get; } = new();
    public Vec3b?[] Vecs { get; }
}

