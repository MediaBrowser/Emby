using MediaBrowser.Controller.Dlna;

namespace MediaBrowser.Dlna.Profiles
{
    public class DenonAvrProfile : DefaultProfile
    {
        public DenonAvrProfile()
        {
            Name = "Denon AVR";

            ProfileId = "denonavr";

            Identification = new DeviceIdentification
            {
                FriendlyName = @"Denon:\[AVR:.*",
                Manufacturer = "Denon"
            };

            DirectPlayProfiles = new[]
            {
                new DirectPlayProfile
                {
                    Container = "mp3,flac,m4a,wma",
                    Type = DlnaProfileType.Audio
                },
            };
        }
    }
}
