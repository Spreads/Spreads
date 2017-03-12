using Spreads.Utils.FastMember;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Spreads.DataTypes {

    public class TypeFlattenner {

        private enum Layout {

            /// <summary>
            /// Public properties as seen by fast-member library
            /// </summary>
            PublicProperties = 0,

            /// <summary>
            /// A single column
            /// </summary>
            Scalar,

            /// <summary>
            /// Types with members that have a DataMember attribute, sort by order, then by name, use Name as a column name
            /// </summary>
            DataContract,
        }

        private Layout _layout;
        private TypeAccessor _accessor;
        private List<Member> _orderedMembers;
        private int _width;
        private MemberSet _members;
        private List<string> _columns = new List<string>();

        public TypeFlattenner(Type type) {
            _accessor = TypeAccessor.Create(type);

            var typeEnum = VariantHelper.GetTypeEnum(type);
            if ((int)typeEnum <= Variant.KnownSmallTypesLimit) {
                _layout = Layout.Scalar;
            }

            _members = _accessor.GetMembers();
            for (int i = 0; i < _members.Count; i++) {
                if (_members[i].IsDefined(typeof(DataMemberAttribute))) {
                    _layout = Layout.DataContract;
                    _orderedMembers = _orderedMembers ?? new List<Member>();
                    _orderedMembers.Add(_members[i]);
                    var memberTypeEnum = VariantHelper.GetTypeEnum(_members[i].Type);
                    if ((int)memberTypeEnum >= Variant.KnownSmallTypesLimit
                        && memberTypeEnum != TypeEnum.String) {
                        throw new NotSupportedException("Only scalar data members are supported");
                    }
                }
            }
            if (_layout == Layout.Scalar) {
                _width = 1;
                _columns.Add(type.Name);
            } else if (_layout == Layout.PublicProperties) {
                _width = _members.Count;
                _orderedMembers = _members.OrderBy(m => m.Name).ToList();
                foreach (var m in _orderedMembers) {
                    _columns.Add(m.Name);
                }
            } else if (_layout == Layout.DataContract) {
                int getorder(Member m)
                {
                    var order = ((DataMemberAttribute)m.GetAttribute(typeof(DataMemberAttribute), true)).Order;
                    if (order <= 0) order = int.MaxValue;
                    return order;
                };
                string getName(Member m)
                {
                    var name = ((DataMemberAttribute)m.GetAttribute(typeof(DataMemberAttribute), true)).Name;
                    if (string.IsNullOrEmpty(name)) {
                        name = m.Name;
                    }
                    return name;
                };

                _orderedMembers = _orderedMembers
                    .OrderBy(getorder)
                    .ThenBy(getName)
                    .ToList();
                _width = _orderedMembers.Count;

                foreach (var m in _orderedMembers) {
                    _columns.Add(getName(m));
                }
            }
        }

        public int Width => _width;
        public List<string> Columns => _columns;

        public void Flatten(object value, ref object[] flattennedValues, int offset = 0) {
            if (flattennedValues == null) {
                flattennedValues = new object[_width];
            }
            // it's a caller's job to do Array.Clear(flattennedValues, offset, _width);
            switch (_layout) {
                case Layout.Scalar:
                    flattennedValues[offset] = value;
                    break;

                case Layout.PublicProperties:
                case Layout.DataContract:
                    for (int i = 0; i < _orderedMembers.Count; i++) {
                        flattennedValues[offset + i] = _accessor[value, _orderedMembers[i].Name];
                    }
                    break;

                default:
                    throw new InvalidOperationException("Invalid layout enum");
            }
        }
    }
}