﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

#if !NET20
#endif

namespace Spreads.Utils.FastMember
{
    /// <summary>
    /// Represents an abstracted view of the members defined for a type
    /// </summary>
    public sealed class MemberSet : IEnumerable<Member>, IList<Member>
    {
        private Member[] members;

        internal MemberSet(Type type)
        {
            const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
#if NET20
            RefList<MemberInfo> properties = new RefList<MemberInfo>(type.GetProperties(PublicInstance));
            properties.AddRange(new RefList<MemberInfo>(type.GetFields(PublicInstance)));
            properties.Sort((p1, p2) => p1.Name.CompareTo(p2.Name));
            members = properties.ConvertAll<Member>(mi => new Member(mi)).ToArray();
#else

            
            members = type.GetProperties(PublicInstance).Cast<MemberInfo>()
                .Concat(type.GetFields(PublicInstance).Cast<MemberInfo>())
                .OrderBy(x => Marshal.OffsetOf(type, x.Name).ToInt32())
                .Select(member => new Member(member)).ToArray();
#endif
        }

        /// <summary>
        /// Return a sequence of all defined members
        /// </summary>
        public IEnumerator<Member> GetEnumerator()
        {
            foreach (var member in members) yield return member;
        }

        /// <summary>
        /// Get a member by index
        /// </summary>
        public Member this[int index]
        {
            get { return members[index]; }
        }

        /// <summary>
        /// The number of members defined for this type
        /// </summary>
        public int Count { get { return members.Length; } }

        Member IList<Member>.this[int index]
        {
            get { return members[index]; }
            set { throw new NotSupportedException(); }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        bool ICollection<Member>.Remove(Member item)
        {
            throw new NotSupportedException();
        }

        void ICollection<Member>.Add(Member item)
        {
            throw new NotSupportedException();
        }

        void ICollection<Member>.Clear()
        {
            throw new NotSupportedException();
        }

        void IList<Member>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        void IList<Member>.Insert(int index, Member item)
        {
            throw new NotSupportedException();
        }

        bool ICollection<Member>.Contains(Member item)
        {
#if NET20
            foreach (var _item in members)
            {
                if (item == _item)
                {
                    return true;
                }
            }
            return false;
#else
            return members.Contains(item);
#endif
        }

        void ICollection<Member>.CopyTo(Member[] array, int arrayIndex)
        {
            members.CopyTo(array, arrayIndex);
        }

        bool ICollection<Member>.IsReadOnly { get { return true; } }

        int IList<Member>.IndexOf(Member member)
        {
            return Array.IndexOf<Member>(members, member);
        }
    }

    /// <summary>
    /// Represents an abstracted view of an individual member defined for a type
    /// </summary>
    public sealed class Member
    {
        private readonly MemberInfo member;

        internal Member(MemberInfo member)
        {
            this.member = member;
        }

        /// <summary>
        /// The name of this member
        /// </summary>
        public string Name { get { return member.Name; } }

        /// <summary>
        /// The type of value stored in this member
        /// </summary>
        public Type Type
        {
            get
            {
                if (member is FieldInfo) return ((FieldInfo)member).FieldType;
                if (member is PropertyInfo) return ((PropertyInfo)member).PropertyType;
                throw new NotSupportedException(member.GetType().Name);
            }
        }

        /// <summary>
        /// Is the attribute specified defined on this type
        /// </summary>
        public bool IsDefined(Type attributeType)
        {
            if (attributeType == null) throw new ArgumentNullException(nameof(attributeType));
            return Attribute.IsDefined(member, attributeType);
        }

        /// <summary>
        /// Getting Attribute Type
        /// </summary>
        public Attribute GetAttribute(Type attributeType, bool inherit)
        {
            return Attribute.GetCustomAttribute(member, attributeType, inherit);
        }

        /// <summary>
        /// Property Can Write
        /// </summary>
        public bool CanWrite
        {
            get
            {
                switch (member.MemberType)
                {
                    case MemberTypes.Property: return ((PropertyInfo)member).CanWrite;
                    default: throw new NotSupportedException(member.MemberType.ToString());
                }
            }
        }

        /// <summary>
        /// Property Can Read
        /// </summary>
        public bool CanRead
        {
            get
            {
                switch (member.MemberType)
                {
                    case MemberTypes.Property: return ((PropertyInfo)member).CanRead;
                    default: throw new NotSupportedException(member.MemberType.ToString());
                }
            }
        }
    }
}