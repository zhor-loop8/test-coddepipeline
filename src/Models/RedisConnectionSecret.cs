namespace WebAPI.Models
{
    public class RedisConnectionSecret
    {
        public string Key { get; set; }
        public string Endpoint { get; set; }
        public bool Ssl { get; set; }

    }
}
