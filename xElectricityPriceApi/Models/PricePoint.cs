﻿using xElectricityPriceApiShared.Model;

namespace xElectricityPriceApi.Models
{
    
    public enum PriceDescription : int
    {
        Unkown = 0,
        Cheap,
        Normal,
        Expensive
    }

    public class PriceInformation : PricePoint, IEquatable<PriceInformation>
    {
        public new Guid Id { get; set; }
        public int StartHour { get; set; }

        public bool Equals(PriceInformation? other)
        {
            if (ReferenceEquals(null, other))
                return false;

            return Id.Equals(other.Id);
        }

        public bool Equals(ExtendedPriceInformation? other)
        {
            if (ReferenceEquals(null, other))
                return false;

            return Id.Equals(other.Id);
        }
    }

    public class ExtendedPriceInformation : PriceInformation
    {
        public double PriceAfterSupport { get; set; }

        public PriceDescription PriceDescription { get; set; }
    }
}
