using OpenCvSharp;

namespace Processing
{
    public class Find
    {
        public static KeyPoint[] Circles(Mat img)
        {
            var p = new SimpleBlobDetector.Params
            {
                FilterByColor = true,
                BlobColor = 255,
            };

            using var sbd = SimpleBlobDetector.Create(p);


            return sbd.Detect(img);
        }
    }
}
