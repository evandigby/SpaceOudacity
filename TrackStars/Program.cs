// See https://aka.ms/new-console-template for more information
using OpenCvSharp;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

var p = new SimpleBlobDetector.Params
{
    FilterByColor = true,
    FilterByCircularity = true,
    FilterByConvexity = false,
    FilterByArea = false,
    FilterByInertia = true,
    MinArea = 50,
    MinConvexity = 0.95f,
    MinDistBetweenBlobs = 1,
    BlobColor = 255,
};

using var sbd = SimpleBlobDetector.Create(p);

var inputFolder = @"E:\SpaceApps\Nebula";

var outFolder = Path.Combine(inputFolder, "out");

Directory.CreateDirectory(outFolder);

//var fileToMat = new TransformBlock<string, MatFile>((fileName) =>
//{
//    var outFile = Path.Combine(outFolder, Path.GetFileName(fileName));
//    return new MatFile(fileName, outFile, Cv2.ImRead(fileName, ImreadModes.Color));
//});

var matDetectBlobs = new TransformBlock<MatFile, MatFileBlobs>((matFile) =>
{
    var rows = matFile.Mat.Rows;
    var cols = matFile.Mat.Cols;

    var typ = matFile.Mat.Type();

    var greyscaleImageMat = new Mat(rows, cols, MatType.CV_8UC1);

    Cv2.ExtractChannel(matFile.Mat, greyscaleImageMat, 0);

    var blobs = sbd.Detect(greyscaleImageMat);

    return new MatFileBlobs(matFile, greyscaleImageMat, blobs.ToList());
},
new ExecutionDataflowBlockOptions
{
    MaxDegreeOfParallelism = 128,
});

var matDrawBlobs = new TransformBlock<MatFileBlobs, MatFileBlobsImage>((matFile) =>
{
    var input = InputArray.Create(matFile.Greyscale);
    var output = InputOutputArray.Create(matFile.Greyscale) ?? throw new Exception("null output");

    Cv2.DrawKeypoints(input, matFile.Blobs, output, color: Scalar.Red, flags: DrawMatchesFlags.DrawRichKeypoints);

    return new MatFileBlobsImage(matFile, output.GetMat()!);
});

var matToCentroidBatch = new BatchBlock<MatFileBlobs>(100);

var matToCentroid = new TransformBlock<MatFileBlobs[], MatFileBlobCentroid>(mats =>
    new MatFileBlobCentroid(
        mats,
        new CentroidsBatch(mats.ToDictionary(m => m.Mat.File, m => m.Blobs.Select(b => new Centroid(b.Pt.X, b.Pt.Y, b.Size)).ToList()))));

var centroidBatchToFile = new TransformManyBlock<MatFileBlobCentroid, MatFileBlobs>(mats =>
{
    var ordered = mats.Centroids.FrameCentroids.Keys.Order();

    var first = ordered.First();
    var last = ordered.Last();

    var fileName = $"{first}-{last}.json";

    var data = JsonSerializer.Serialize(mats.Centroids.FrameCentroids);

    var outFile = Path.Combine(outFolder, fileName);

    File.WriteAllText(outFile, data);

    return mats.Mat;
});

var matcher = new BFMatcher(NormTypes.Hamming);

//var matToFile = new TransformBlock<MatFileBlobsImage, MatFileBlobsImage>(
//    matFile =>
//    {
//        Cv2.ImWrite(matFile.Mat.Mat.OutputFile, matFile.Output);
//        return matFile;
//    },
//    new ExecutionDataflowBlockOptions
//    {
//        MaxDegreeOfParallelism = 10,
//    });

var cleanup = new ActionBlock<MatFileBlobs>(matFile =>
{
    matFile.Mat.Mat.Dispose();
    matFile.Greyscale.Dispose();

    //matFile.Output.Dispose();
    //matFile.Mat.Greyscale.Dispose();
    //matFile.Mat.Mat.Mat.Dispose();
});


//fileToMat.LinkTo(
//    matDetectBlobs,
//    new DataflowLinkOptions
//    {
//        PropagateCompletion = true,
//    });

matDetectBlobs.LinkTo(
    matToCentroidBatch,
    new DataflowLinkOptions
    {
        PropagateCompletion = true,
    });

matToCentroidBatch.LinkTo(
    matToCentroid,
    new DataflowLinkOptions
    {
        PropagateCompletion = true,
    });

matToCentroid.LinkTo(
    centroidBatchToFile,
    new DataflowLinkOptions
    {
        PropagateCompletion = true,
    });

centroidBatchToFile.LinkTo(
    cleanup,
    new DataflowLinkOptions
    {
        PropagateCompletion = true,
    });

//matDrawBlobs.LinkTo(
//    matToFile,
//    new DataflowLinkOptions
//    {
//        PropagateCompletion = true,
//    });

//matToFile.LinkTo(
//    cleanup,
//    new DataflowLinkOptions
//    {
//        PropagateCompletion = true,
//    });

var capture = VideoCapture.FromFile(@"E:\SpaceApps\NebulaVideo.mov");

while (true)
{
    var currentFrame = capture.PosFrames;
    var img = capture.RetrieveMat();

    if (img.Empty()) break;

    var fileName = $"{currentFrame}";
    await matDetectBlobs.SendAsync(new MatFile(fileName, img));
}

//var files = Directory.EnumerateFiles(inputFolder);

//foreach (var file in files.Skip(30).Take(10))
//{
//    await fileToMat.SendAsync(file);
//}

matDetectBlobs.Complete();

await cleanup.Completion;

internal record MatFile(string File, Mat Mat);

internal record MatFileBlobs(MatFile Mat, Mat Greyscale, IReadOnlyList<KeyPoint> Blobs);

internal record MatFileBlobsImage(MatFileBlobs Mat, Mat Output);

internal record MatFileBlobCentroid(MatFileBlobs[] Mat, CentroidsBatch Centroids);

internal record Centroid(float X, float Y, float Size);

internal record CentroidsBatch(Dictionary<string, List<Centroid>> FrameCentroids);