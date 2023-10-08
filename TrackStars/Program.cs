// See https://aka.ms/new-console-template for more information
using OpenCvSharp;
using System.Threading.Tasks.Dataflow;

using var orb = ORB.Create(500);

using var bf = new BFMatcher(NormTypes.Hamming);

var inputFolder = @"E:\SpaceApps\Nebula";

var outFolder = Path.Combine(inputFolder, "out");

Directory.CreateDirectory(outFolder);

var matDetectBlobs = new TransformBlock<MatFile, MatFileBlobs>((matFile) =>
{
    var rows = matFile.Mat.Rows;
    var cols = matFile.Mat.Cols;

    var typ = matFile.Mat.Type();

    var greyscaleImageMat = new Mat(rows, cols, MatType.CV_8UC1);

    Cv2.ExtractChannel(matFile.Mat, greyscaleImageMat, 0);

    var descriptors = new Mat();

    orb.DetectAndCompute(greyscaleImageMat, null, out var keypoints, descriptors);

    return new MatFileBlobs(matFile, greyscaleImageMat, descriptors, keypoints.ToList());
},
new ExecutionDataflowBlockOptions
{
    MaxDegreeOfParallelism = 16,
});

var matDrawBlobs = new TransformBlock<MatFileBlobs, MatFileBlobsImage>((matFile) =>
{
    var input = InputArray.Create(matFile.Greyscale);
    var output = InputOutputArray.Create(matFile.Greyscale) ?? throw new Exception("null output");

    Cv2.DrawKeypoints(input, matFile.KeyPoints, output, color: Scalar.Red, flags: DrawMatchesFlags.DrawRichKeypoints);
    return new MatFileBlobsImage(matFile, output.GetMat()!);
},
new ExecutionDataflowBlockOptions
{
    MaxDegreeOfParallelism = 16,
});

var matSlidingWindow = CreateSlidingWindow<MatFileBlobs>(2);

var detectMatches = new TransformBlock<MatFileBlobs[], MatFileBlobsMatches>(mats =>
{
    if (mats.Length != 2)
    {
        throw new Exception("bad mats length");
    }

    var f1 = mats[0];
    var f2 = mats[1];

    var des1 = f1.Descriptors;
    var des2 = f2.Descriptors;

    var matches = bf.Match(des1, des2);

    var sorted = matches
        .OrderBy(m => m.Distance)
        .Take(50)
        .ToArray();

    return new MatFileBlobsMatches(f1, f2, sorted);
},
new ExecutionDataflowBlockOptions
{
    MaxDegreeOfParallelism = 16,
});

var matchesToFile = new TransformBlock<MatFileBlobsMatches, MatFileBlobs>(mats =>
{
    var outImg = new Mat();

    Cv2.DrawMatches(mats.F1.Greyscale, mats.F1.KeyPoints, mats.F2.Greyscale, mats.F2.KeyPoints, mats.Matches, outImg, Scalar.Red, flags: DrawMatchesFlags.DrawRichKeypoints);

    var sampleImage = $"{mats.F1.Mat.File}-{mats.F2.Mat.File}.tiff";

    Cv2.ImWrite(Path.Combine(outFolder, sampleImage), outImg);

    return mats.F1;
},
new ExecutionDataflowBlockOptions
{
    MaxDegreeOfParallelism = 16,
});

var cleanup = new ActionBlock<MatFileBlobs>(matFile =>
{
    matFile.Mat.Mat.Dispose();
    matFile.Greyscale.Dispose();
    matFile.Descriptors.Dispose();
});


var dflo = new DataflowLinkOptions
{
    PropagateCompletion = true,
};

matDetectBlobs.LinkTo(matSlidingWindow, dflo);
matSlidingWindow.LinkTo(detectMatches, dflo);
detectMatches.LinkTo(matchesToFile, dflo);
matchesToFile.LinkTo(cleanup, dflo);


var capture = VideoCapture.FromFile(@"E:\SpaceApps\NebulaVideo.mov");

while (true)
{
    var currentFrame = capture.PosFrames;
    var img = capture.RetrieveMat();

    if (img.Empty()) break;

    var fileName = $"{currentFrame}";
    await matDetectBlobs.SendAsync(new MatFile(fileName, img));
}

matDetectBlobs.Complete();

await cleanup.Completion;

IPropagatorBlock<T, T[]> CreateSlidingWindow<T>(int windowSize)
{
    // Create a queue to hold messages.
    var queue = new Queue<T>();

    // The source part of the propagator holds arrays of size windowSize
    // and propagates data out to any connected targets.
    var source = new BufferBlock<T[]>();

    // The target part receives data and adds them to the queue.
    var target = new ActionBlock<T>(item =>
    {
        // Add the item to the queue.
        queue.Enqueue(item);
        // Remove the oldest item when the queue size exceeds the window size.
        if (queue.Count > windowSize)
            queue.Dequeue();
        // Post the data in the queue to the source block when the queue size
        // equals the window size.
        if (queue.Count == windowSize)
            source.Post(queue.ToArray());
    });

    // When the target is set to the completed state, propagate out any
    // remaining data and set the source to the completed state.
    target.Completion.ContinueWith(delegate
    {
        if (queue.Count > 0 && queue.Count < windowSize)
            source.Post(queue.ToArray());
        source.Complete();
    });

    // Return a IPropagatorBlock<T, T[]> object that encapsulates the
    // target and source blocks.
    return DataflowBlock.Encapsulate(target, source);
}


internal record MatFile(string File, Mat Mat);

internal record MatFileBlobs(MatFile Mat, Mat Greyscale, Mat Descriptors, IReadOnlyList<KeyPoint> KeyPoints);

internal record MatFileBlobsMatches(MatFileBlobs F1, MatFileBlobs F2, DMatch[] Matches);

internal record MatFileBlobsImage(MatFileBlobs Mat, Mat Output);
