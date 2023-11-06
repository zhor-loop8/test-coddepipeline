namespace WebAPI.Models.UserFileBackup
{
    public class UserFile
    {
        public string Filename { get; set; } 
        public string ContentType { get; set; }
        public byte[] Content { get; set; }
    }
}
