using OpenCvSharp;

namespace Processing
{
    public class Find
    {
        public static KeyPoint[] Circles(InputArray inputArray)
        {
            var p = new SimpleBlobDetector.Params
            {
                FilterByColor = false,

            };

            using var sbd = SimpleBlobDetector.Create(p);


            return sbd.Detect(inputArray.GetMat());
        }
    }
}
