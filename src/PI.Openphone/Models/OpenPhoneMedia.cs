namespace PI.Openphone.Models;

public class OpenPhoneMedia
{
    //     "url" : "https://storage.googleapis.com/opstatics/83f619906d664d6eab5d3a4e0152e095.mp3",
    public string Url { get; set; }
    
    //     "type" : "audio/mpeg",
    public string Type { get; set; }
    
    //     "duration" : NumberLong(1)
    public int? Duration { get; set; }
}
