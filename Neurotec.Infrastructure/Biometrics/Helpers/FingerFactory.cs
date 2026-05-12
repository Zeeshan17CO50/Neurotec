using Neurotec.Biometrics;

namespace Neurotec.Infrastructure.Biometrics.Helpers
{
    public static class FingerFactory
    {
        public static List<NFinger> CreateFingers(params NFPosition[] positions)
        {
            return positions.Select(position => new NFinger
            {
                Position = position,
                ImpressionType = NFImpressionType.LiveScanPlain
            }).ToList();
        }

        public static List<NPalm> CreatePalms(params NFPosition[] positions)
        {
            return positions.Select(position => new NPalm
            {
                Position = position,
                //ImpressionType = NFImpressionType.LiveScanPalm
            }).ToList();
        }
    }
}
