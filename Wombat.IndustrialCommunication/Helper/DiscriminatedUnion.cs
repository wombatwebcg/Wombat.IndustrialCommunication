using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Wombat.IndustrialCommunication
{

    public enum DiscriminatedUnionOption
    {
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "A")] A,

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "B")] B
    }

    public class DiscriminatedUnion<TA, TB>
    {
        private TA optionA;
        private TB optionB;
        private DiscriminatedUnionOption option;

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "A")]
        public TA A
        {
            get
            {
                if (this.Option != DiscriminatedUnionOption.A)
                    throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture,
                        "{0} is not a valid option for this discriminated union instance.", DiscriminatedUnionOption.A));

                return this.optionA;
            }
        }

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "B")]
        public TB B
        {
            get
            {
                if (this.Option != DiscriminatedUnionOption.B)
                    throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture,
                        "{0} is not a valid option for this discriminated union instance.", DiscriminatedUnionOption.B));

                return this.optionB;
            }
        }

        public DiscriminatedUnionOption Option
        {
            get { return this.option; }
        }

        [SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes",
Justification = "Factory method.")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "0#a")]
        public static DiscriminatedUnion<TA, TB> CreateA(TA a)
        {
            return new DiscriminatedUnion<TA, TB>() { option = DiscriminatedUnionOption.A, optionA = a };
        }

        [SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes",
Justification = "Factory method.")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "0#b")]
        public static DiscriminatedUnion<TA, TB> CreateB(TB b)
        {
            return new DiscriminatedUnion<TA, TB>() { option = DiscriminatedUnionOption.B, optionB = b };
        }

        public override string ToString()
        {
            string value = null;
            switch (Option)
            {
                case DiscriminatedUnionOption.A:
                    value = A.ToString();
                    break;
                case DiscriminatedUnionOption.B:
                    value = B.ToString();
                    break;
            }

            return value;
        }
    }
}
