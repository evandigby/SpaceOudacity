// See https://aka.ms/new-console-template for more information
using OpenCvSharp;

const int maxFrames = 7 * 30;

using var orb = ORB.Create(5000, 1.2f, 8, 20, 0, 4, ORBScoreType.Harris, 20, 10);

var p = new SimpleBlobDetector.Params
{
    FilterByColor = true,
    BlobColor = 255,
    MinArea = 1,
};

using var bf = new BFMatcher(NormTypes.Hamming2);

using var sbd = SimpleBlobDetector.Create(p);

var matchObjects = new List<MatchObject>();

var secondToFirst = new Dictionary<int, int>();

using (var capture = VideoCapture.FromFile(@"E:\SpaceApps\NebulaVideo.mov"))
{
    using var greyscaleImageMat = new Mat();
    using var blur = new Mat();
    using var thresh = new Mat();
    using var outImg = new Mat();
    var descriptorsLast = new Mat();
    KeyPoint[]? keypointsLast = null;

    using var output = new VideoWriter(@"E:\SpaceApps\NebulaVideoOutputDetection.avi", FourCC.WMV1, capture.Fps, new Size(capture.FrameWidth, capture.FrameHeight), true);

    for (int i = 0; i < maxFrames; i++)
    {
        var currentFrame = capture.PosFrames;
        using var img = capture.RetrieveMat();
        if (img.Empty()) break;

        Cv2.ExtractChannel(img, greyscaleImageMat, 0);
        Cv2.GaussianBlur(greyscaleImageMat, blur, new Size(5, 5), 0);
        Cv2.Threshold(blur, thresh, 200, 255, ThresholdTypes.Binary);

        var keypoints = sbd.Detect(thresh);

        var descriptors = new Mat();
        orb.Compute(thresh, ref keypoints, descriptors);
        Cv2.DrawKeypoints(thresh, keypoints, outImg, Scalar.Red, DrawMatchesFlags.DrawRichKeypoints);

        var newSecondToFirst = new Dictionary<int, int>();

        if (i > 0 && keypointsLast != null)
        {
            var matches = bf.Match(descriptorsLast, descriptors).OrderBy(m => m.Distance).DistinctBy(d => d.TrainIdx).ToArray();

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
                matchObject.Index[frame2] = match.TrainIdx;
                matchObject.KeyPoints[frame2] = keypoints[match.TrainIdx];
                newSecondToFirst[match.TrainIdx] = matchObjectIndex;
            }

            output.Write(outImg);
        }

        secondToFirst = newSecondToFirst;
        descriptorsLast = descriptors;
        keypointsLast = keypoints;
        Console.WriteLine($"Detected frame {i}");
    }
}

var matchObjectWithAllFrames = matchObjects.OrderByDescending(mo => mo.KeyPoints.Count(kp => kp is not null)).Take(10);

var test = matchObjects.OrderByDescending(mo => mo.KeyPoints.Count(kp => kp is not null)).Take(10).Select(i => new { Count = i.KeyPoints.Count(kp => kp is not null), i.KeyPoints }).ToArray();


using (var capture = VideoCapture.FromFile(@"E:\SpaceApps\NebulaVideo.mov"))
{
    using var output = new VideoWriter(@"E:\SpaceApps\NebulaVideoOutputMatched.avi", FourCC.WMV1, capture.Fps, new Size(capture.FrameWidth, capture.FrameHeight), true);
    for (int i = 0; i < maxFrames; i++)
    {
        var currentFrame = capture.PosFrames;
        using var img = capture.RetrieveMat();
        if (img.Empty()) break;

        using var outImg = new Mat();

        var matchCount = matchObjectWithAllFrames.Where(o => o.KeyPoints[i] is not null).Select(o => o.KeyPoints[i]).Cast<KeyPoint>();

        if (matchCount.Count() > 0)
        {
            Cv2.DrawKeypoints(img, matchCount, outImg, Scalar.Purple, DrawMatchesFlags.DrawRichKeypoints);
            output.Write(outImg);
        }
        else
        {
            output.Write(img);
        }
        Console.WriteLine($"Wrote frame {i}");
    }
}

//Console.WriteLine(matchesBag.Count);

//// From: https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/walkthrough-creating-a-custom-dataflow-block-type
//IPropagatorBlock<T, T[]> CreateSlidingWindow<T>(int windowSize)
//{
//    // Create a queue to hold messages.
//    var queue = new Queue<T>();

//    // The source part of the propagator holds arrays of size windowSize
//    // and propagates data out to any connected targets.
//    var source = new BufferBlock<T[]>();

//    // The target part receives data and adds them to the queue.
//    var target = new ActionBlock<T>(item =>
//    {
//        // Add the item to the queue.
//        queue.Enqueue(item);
//        // Remove the oldest item when the queue size exceeds the window size.
//        if (queue.Count > windowSize)
//            queue.Dequeue();
//        // Post the data in the queue to the source block when the queue size
//        // equals the window size.
//        if (queue.Count == windowSize)
//            source.Post(queue.ToArray());
//    });

//    // When the target is set to the completed state, propagate out any
//    // remaining data and set the source to the completed state.
//    target.Completion.ContinueWith(delegate
//    {
//        if (queue.Count > 0 && queue.Count < windowSize)
//            source.Post(queue.ToArray());
//        source.Complete();
//    });

//    // Return a IPropagatorBlock<T, T[]> object that encapsulates the
//    // target and source blocks.
//    return DataflowBlock.Encapsulate(target, source);
//}


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
    }

    public int[] Index { get; }
    public KeyPoint?[] KeyPoints { get; }
}



//var inputFolder = @"E:\SpaceApps\Nebula";

//var outFolder = Path.Combine(inputFolder, "out");

//Directory.CreateDirectory(outFolder);

//int totalFrames = 0;

//var matDetectBlobs = new TransformBlock<MatFrame, MatFrameBlobs>((matFile) =>
//{
//    var rows = matFile.Mat.Rows;
//    var cols = matFile.Mat.Cols;

//    var typ = matFile.Mat.Type();

//    using var greyscaleImageMat = new Mat();
//    using var blur = new Mat();
//    var thresh = new Mat();
//    var descriptors = new Mat();

//    Cv2.ExtractChannel(matFile.Mat, greyscaleImageMat, 0);
//    Cv2.GaussianBlur(greyscaleImageMat, blur, new Size(5, 5), 0);
//    Cv2.Threshold(blur, thresh, 200, 255, ThresholdTypes.Binary);

//    var keypoints = sbd.Detect(thresh);
//    orb.Compute(thresh, ref keypoints, descriptors);
//    //orb.DetectAndCompute(thresh, null, out var keypoints, descriptors);

//    return new MatFrameBlobs(matFile, thresh, descriptors, keypoints.ToList());
//},
//new ExecutionDataflowBlockOptions
//{
//    MaxDegreeOfParallelism = 16,
//});

//var matDrawBlobs = new TransformBlock<MatFrameBlobs, MatFrameBlobsImage>((matFile) =>
//{
//    var input = InputArray.Create(matFile.Greyscale);
//    var output = InputOutputArray.Create(matFile.Greyscale) ?? throw new Exception("null output");

//    Cv2.DrawKeypoints(input, matFile.KeyPoints, output, color: Scalar.Purple, flags: DrawMatchesFlags.DrawRichKeypoints);
//    return new MatFrameBlobsImage(matFile, output.GetMat()!);
//},
//new ExecutionDataflowBlockOptions
//{
//    MaxDegreeOfParallelism = 16,
//});

//var matSlidingWindow = CreateSlidingWindow<MatFrameBlobs>(2);

//var detectMatches = new TransformBlock<MatFrameBlobs[], MatFrameBlobsMatches>(mats =>
//{
//    if (mats.Length != 2)
//    {
//        throw new Exception("bad mats length");
//    }

//    var f1 = mats[0];
//    var f2 = mats[1];

//    var des1 = f1.Descriptors;
//    var des2 = f2.Descriptors;

//    var matches = bf.Match(des1, des2);

//    var sorted = matches
//        .OrderBy(m => m.Distance)
//        //.Take(50)
//        .ToArray();

//    return new MatFrameBlobsMatches(f1, f2, sorted);
//},
//new ExecutionDataflowBlockOptions
//{
//    MaxDegreeOfParallelism = 16,
//});


//var matchesBag = new ConcurrentBag<MatchKeyPoints>();

//var matchesOutput = new TransformBlock<MatFrameBlobsMatches, MatFrameBlobs>(mats =>
//{
//    matchesBag.Add(new MatchKeyPoints(mats.F1.Mat.Frame, mats.F2.Mat.Frame, mats.F1.KeyPoints, mats.F2.KeyPoints, mats.Matches));

//    return mats.F1;
//},
//new ExecutionDataflowBlockOptions
//{
//    MaxDegreeOfParallelism = 16,
//});

//var cleanup = new ActionBlock<MatFrameBlobs>(matFile =>
//{
//    var currentFinished = Interlocked.Increment(ref totalFrames);

//    Console.WriteLine($"Finished {currentFinished} frames");

//    matFile.Mat.Mat.Dispose();
//    matFile.Greyscale.Dispose();
//    matFile.Descriptors.Dispose();
//});


//var dflo = new DataflowLinkOptions
//{
//    PropagateCompletion = true,
//};

//matDetectBlobs.LinkTo(matSlidingWindow, dflo);
//matSlidingWindow.LinkTo(detectMatches, dflo);
//detectMatches.LinkTo(matchesOutput, dflo);
//matchesOutput.LinkTo(cleanup, dflo);

//using (var capture = VideoCapture.FromFile(@"E:\SpaceApps\NebulaVideo.mov"))
//{
//    for (int i = 0; i < maxFrames; i++)
//    //while (true)
//    {
//        var currentFrame = capture.PosFrames;
//        var img = capture.RetrieveMat();

//        if (img.Empty()) break;

//        await matDetectBlobs.SendAsync(new MatFrame(currentFrame, img));
//    }

//    matDetectBlobs.Complete();

//    await cleanup.Completion;
//}

//var matchesList = matchesBag.OrderBy(mkp => mkp.FrameOne).ToList();

//var matchObjects = new List<MatchObject>();

//var secondToFirst = new Dictionary<int, int>();

//for (int i = 0; i < matchesList.Count; i++)
//{
//    var matches = matchesList[i];

//    var newSecondToFirst = new Dictionary<int, int>();
//    foreach (var match in matches.Matches)
//    {
//        MatchObject matchObject;
//        if (secondToFirst.TryGetValue(match.QueryIdx, out var matchObjectIndex))
//        {
//            matchObject = matchObjects[matchObjectIndex];

//            if (matchObject.Index[matches.FrameOne] != match.QueryIdx)
//            {
//                throw new Exception("Algorithm wrong");
//            }
//        }
//        else
//        {
//            matchObject = new MatchObject(totalFrames + 1);
//            matchObject.Index[matches.FrameOne] = match.QueryIdx;
//            matchObject.KeyPoints[matches.FrameOne] = matches.F1[match.QueryIdx];

//            matchObjectIndex = matchObjects.Count;
//            matchObjects.Add(matchObject);
//        }
//        matchObject.Index[matches.FrameTwo] = match.TrainIdx;
//        matchObject.KeyPoints[matches.FrameTwo] = matches.F2[match.TrainIdx];
//        newSecondToFirst[match.TrainIdx] = matchObjectIndex;
//    }
//    secondToFirst = newSecondToFirst;
//}
