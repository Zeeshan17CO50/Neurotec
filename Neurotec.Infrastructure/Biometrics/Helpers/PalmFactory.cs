using Neurotec.Biometrics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Neurotec.Infrastructure.Biometrics.Helpers
{
    public class PalmFactory
    {
        public static List<NPalm> CreatePalms(params NFPosition[] positions)
        {
            return positions.Select(position => new NPalm
            {
                Position = position,
                ImpressionType = NFImpressionType.LiveScanPlain
            }).ToList();
        }
    }
}
