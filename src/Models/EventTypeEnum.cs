namespace WebAPI.Models
{
    public enum EventTypeEnum : byte
    {
        Pairing = 1,
        Keydist,
        KeyReq,
        KeyReqReply,
        ClientMessage,
        UserMessage
       
    }
}
