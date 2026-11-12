using System;
using System.Text;
using NUnit.Framework;
using Shooter.Generation;

namespace Shooter.Tests.EditMode
{
    public sealed class GenerationPrimitiveTests
    {
        [Test]
        public void PcgAndNamedStreamMatchPinnedVectorsInUnity()
        {
            var pcg = new Pcg32(42, 54);
            foreach (uint expected in new uint[] { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e })
                Assert.That(pcg.NextUInt(), Is.EqualTo(expected));
            var stream = NamedRandomStream.Derive(460072030, "fixtures/box", "wear/00");
            Assert.That(stream.StreamId.ToString(), Is.EqualTo("1fd3becc011e75a06d2a89a85426ccd335259b27a1be04d7b882b4dd94a5dedf"));
        }

        [Test]
        public void CanonicalBytesAndGuidMatchV1InUnity()
        {
            Assert.That(Encoding.UTF8.GetString(CanonicalJson.Encode(Encoding.UTF8.GetBytes("{\"z\":2,\"a\":1}"))), Is.EqualTo("{\"a\":1,\"z\":2}"));
            Assert.That(ContentIdentity.UnityGuid("fixtures/box/mesh/lod0"), Is.EqualTo("47f902c4ab9b44b0b53e9f5553244acf"));
            Assert.Throws<FormatException>(() => CanonicalJson.Encode(Encoding.UTF8.GetBytes("{\"a\":1,\"a\":2}")));
        }
    }
}
