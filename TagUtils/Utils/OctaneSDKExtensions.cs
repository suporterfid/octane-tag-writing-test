using Impinj.OctaneSdk;
using Impinj.TagUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Impinj.Utils
{
    public static class OctaneSDKExtensions
    {
        public static MemoryBank ToOctaneSDKMemoryBank(this TagAccessLocation tagAccessLocation)
        {
            switch (tagAccessLocation)
            {
                case TagAccessLocation.Reserved:
                case TagAccessLocation.KillPassword:
                case TagAccessLocation.AccessPassword:
                    return MemoryBank.Reserved;
                case TagAccessLocation.Epc:
                case TagAccessLocation.Tid:
                    return (MemoryBank)tagAccessLocation;
                case TagAccessLocation.User:
                    return MemoryBank.User;
                default:
                    throw new Exception("Unsupported TagReadOp! '" + tagAccessLocation.ToString() + "'");
            }
        }

        public static string ToEnumMemberAttrValue(this Enum @enum)
        {
            MemberInfo? memberInfo = @enum.GetType().GetMember(@enum.ToString()).FirstOrDefault();
            EnumMemberAttribute? enumMemberAttribute = memberInfo?.GetCustomAttributes(false).OfType<EnumMemberAttribute>().FirstOrDefault();
            return enumMemberAttribute?.Value ?? @enum.ToString();
        }

        public static T FromEnumMemberAttrValue<T>(this string str) where T : struct, Enum
        {
            Type enumType = typeof(T);
            var name = Enum.GetNames(enumType)
                .FirstOrDefault(n =>
                {
                    var field = enumType.GetField(n);
                    var attr = field?.GetCustomAttributes(typeof(EnumMemberAttribute), true)
                        .OfType<EnumMemberAttribute>()
                        .SingleOrDefault();
                    return attr?.Value == str;
                });
            return name != null ? (T)Enum.Parse(enumType, name) : default;
        }
    }
}
