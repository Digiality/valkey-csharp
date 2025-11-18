namespace Valkey.Commands;

/// <summary>
/// Static byte arrays for Redis/Valkey commands to avoid allocations.
/// </summary>
internal static class CommandBytes
{
    // String commands
    public static readonly byte[] Get = "GET"u8.ToArray();
    public static readonly byte[] Set = "SET"u8.ToArray();
    public static readonly byte[] Incr = "INCR"u8.ToArray();
    public static readonly byte[] Incrby = "INCRBY"u8.ToArray();
    public static readonly byte[] Decr = "DECR"u8.ToArray();
    public static readonly byte[] Decrby = "DECRBY"u8.ToArray();
    public static readonly byte[] Append = "APPEND"u8.ToArray();
    public static readonly byte[] Strlen = "STRLEN"u8.ToArray();

    // Hash commands
    public static readonly byte[] Hget = "HGET"u8.ToArray();
    public static readonly byte[] Hset = "HSET"u8.ToArray();
    public static readonly byte[] Hdel = "HDEL"u8.ToArray();
    public static readonly byte[] Hexists = "HEXISTS"u8.ToArray();
    public static readonly byte[] Hgetall = "HGETALL"u8.ToArray();
    public static readonly byte[] Hlen = "HLEN"u8.ToArray();
    public static readonly byte[] Hkeys = "HKEYS"u8.ToArray();
    public static readonly byte[] Hvals = "HVALS"u8.ToArray();
    public static readonly byte[] Hincrby = "HINCRBY"u8.ToArray();

    // List commands
    public static readonly byte[] Lpush = "LPUSH"u8.ToArray();
    public static readonly byte[] Rpush = "RPUSH"u8.ToArray();
    public static readonly byte[] Lpop = "LPOP"u8.ToArray();
    public static readonly byte[] Rpop = "RPOP"u8.ToArray();
    public static readonly byte[] Llen = "LLEN"u8.ToArray();
    public static readonly byte[] Lrange = "LRANGE"u8.ToArray();

    // Set commands
    public static readonly byte[] Sadd = "SADD"u8.ToArray();
    public static readonly byte[] Srem = "SREM"u8.ToArray();
    public static readonly byte[] Sismember = "SISMEMBER"u8.ToArray();
    public static readonly byte[] Smembers = "SMEMBERS"u8.ToArray();
    public static readonly byte[] Scard = "SCARD"u8.ToArray();
    public static readonly byte[] Sinter = "SINTER"u8.ToArray();
    public static readonly byte[] Sunion = "SUNION"u8.ToArray();
    public static readonly byte[] Sdiff = "SDIFF"u8.ToArray();

    // Sorted Set commands
    public static readonly byte[] Zadd = "ZADD"u8.ToArray();
    public static readonly byte[] Zrem = "ZREM"u8.ToArray();
    public static readonly byte[] Zscore = "ZSCORE"u8.ToArray();
    public static readonly byte[] Zrange = "ZRANGE"u8.ToArray();
    public static readonly byte[] Zcard = "ZCARD"u8.ToArray();

    // Key commands
    public static readonly byte[] Del = "DEL"u8.ToArray();
    public static readonly byte[] Exists = "EXISTS"u8.ToArray();
    public static readonly byte[] Expire = "EXPIRE"u8.ToArray();
    public static readonly byte[] Ttl = "TTL"u8.ToArray();

    // Transaction commands
    public static readonly byte[] Multi = "MULTI"u8.ToArray();
    public static readonly byte[] Exec = "EXEC"u8.ToArray();
    public static readonly byte[] Discard = "DISCARD"u8.ToArray();

    // Scripting commands
    public static readonly byte[] Eval = "EVAL"u8.ToArray();
    public static readonly byte[] Evalsha = "EVALSHA"u8.ToArray();
    public static readonly byte[] Script = "SCRIPT"u8.ToArray();
    public static readonly byte[] Load = "LOAD"u8.ToArray();
    public static readonly byte[] Exists_Script = "EXISTS"u8.ToArray();
    public static readonly byte[] Flush = "FLUSH"u8.ToArray();

    // Stream commands
    public static readonly byte[] Xadd = "XADD"u8.ToArray();
    public static readonly byte[] Xread = "XREAD"u8.ToArray();
    public static readonly byte[] Xrange = "XRANGE"u8.ToArray();
    public static readonly byte[] Xlen = "XLEN"u8.ToArray();
    public static readonly byte[] Xdel = "XDEL"u8.ToArray();
    public static readonly byte[] Xtrim = "XTRIM"u8.ToArray();
    public static readonly byte[] Xgroup = "XGROUP"u8.ToArray();
    public static readonly byte[] Xreadgroup = "XREADGROUP"u8.ToArray();
    public static readonly byte[] Xack = "XACK"u8.ToArray();
    public static readonly byte[] Mkstream = "MKSTREAM"u8.ToArray();

    // Pub/Sub commands
    public static readonly byte[] Publish = "PUBLISH"u8.ToArray();
    public static readonly byte[] Subscribe = "SUBSCRIBE"u8.ToArray();
    public static readonly byte[] Unsubscribe = "UNSUBSCRIBE"u8.ToArray();
    public static readonly byte[] Psubscribe = "PSUBSCRIBE"u8.ToArray();
    public static readonly byte[] Punsubscribe = "PUNSUBSCRIBE"u8.ToArray();

    // Geospatial commands
    public static readonly byte[] Geoadd = "GEOADD"u8.ToArray();
    public static readonly byte[] Geodist = "GEODIST"u8.ToArray();
    public static readonly byte[] Geohash = "GEOHASH"u8.ToArray();
    public static readonly byte[] Geopos = "GEOPOS"u8.ToArray();
    public static readonly byte[] Georadius = "GEORADIUS"u8.ToArray();
    public static readonly byte[] Georadiusbymember = "GEORADIUSBYMEMBER"u8.ToArray();
    public static readonly byte[] Geosearch = "GEOSEARCH"u8.ToArray();
    public static readonly byte[] Geosearchstore = "GEOSEARCHSTORE"u8.ToArray();

    // Connection commands
    public static readonly byte[] Ping = "PING"u8.ToArray();
    public static readonly byte[] Select = "SELECT"u8.ToArray();
    public static readonly byte[] Auth = "AUTH"u8.ToArray();
    public static readonly byte[] Hello = "HELLO"u8.ToArray();
    public static readonly byte[] Client = "CLIENT"u8.ToArray();
    public static readonly byte[] Setname = "SETNAME"u8.ToArray();

    // Common arguments
    public static readonly byte[] Ex = "EX"u8.ToArray();
    public static readonly byte[] Px = "PX"u8.ToArray();
    public static readonly byte[] Nx = "NX"u8.ToArray();
    public static readonly byte[] Xx = "XX"u8.ToArray();
    public static readonly byte[] Withscores = "WITHSCORES"u8.ToArray();
    public static readonly byte[] Maxlen = "MAXLEN"u8.ToArray();
    public static readonly byte[] Streams = "STREAMS"u8.ToArray();
    public static readonly byte[] Group = "GROUP"u8.ToArray();
    public static readonly byte[] Create = "CREATE"u8.ToArray();
    public static readonly byte[] Destroy = "DESTROY"u8.ToArray();
    public static readonly byte[] Count = "COUNT"u8.ToArray();
    public static readonly byte[] Resp3Version = "3"u8.ToArray();
    public static readonly byte[] DefaultUser = "default"u8.ToArray();

    // Geospatial arguments
    public static readonly byte[] M = "M"u8.ToArray();
    public static readonly byte[] Km = "KM"u8.ToArray();
    public static readonly byte[] Mi = "MI"u8.ToArray();
    public static readonly byte[] Ft = "FT"u8.ToArray();
    public static readonly byte[] Withdist = "WITHDIST"u8.ToArray();
    public static readonly byte[] Withcoord = "WITHCOORD"u8.ToArray();
    public static readonly byte[] Withhash = "WITHHASH"u8.ToArray();
    public static readonly byte[] Fromlonlat = "FROMLONLAT"u8.ToArray();
    public static readonly byte[] Frommember = "FROMMEMBER"u8.ToArray();
    public static readonly byte[] Byradius = "BYRADIUS"u8.ToArray();
    public static readonly byte[] Bybox = "BYBOX"u8.ToArray();
    public static readonly byte[] Bypolygon = "BYPOLYGON"u8.ToArray();
    public static readonly byte[] Asc = "ASC"u8.ToArray();
    public static readonly byte[] Desc = "DESC"u8.ToArray();
}
