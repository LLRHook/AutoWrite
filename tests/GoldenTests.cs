using System;
using System.Security.Cryptography;
using System.Text;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private static void GoldenTests()
        {
            Test("committed full key-and-time fingerprints preserve approved engine behaviour", delegate
            {
                // Captured from the engine at a7978bac, whose approved incoming-key
                // change and zero-default paragraph behaviour were independently compared
                // with baseline 007ae274 (90 profiles/texts/seeds). No engine arithmetic
                // changed while producing these hashes. UTF-8 VirtualRuntime.Log(), SHA-256.
                string[] texts = { "quiet ordinary words and a pause. Another sentence.\nEnd.",
                    "A report [12]: 42.5% (p < 0.05)! R&D\r\nNext line.\r\nLast.",
                    "aB9! [] {} @NASA and/or the study's 128 cases?\nYes: e.g., Table 2.1." };
                string[] expected =
                {
                    "60BE06D3106019B766AEDF7005AAE5802CB5DAF7DD5985A28014CE15248CC5DE",
                    "39D538A5AA1A61FBD274299DC582F80B457502C7AC5D50C74C9B22E14D91B70C",
                    "352D0ED51AD35EE0B380F37A14A6599BFFCFDEE9A1679FFAE4CF50A128378EDD",
                    "F53B7A713F40E21C88966C039A78282BD9060AEE4D250DC71DAE7AF45C38E196",
                    "88607E49EE463E851D69187F2E2909B1C906E4239DDB31436A01194083CA4D2D",
                    "24F714FC132DB96EF6584EBEA289548CF717758577107D94E24EF5B214F415E5",
                    "87B427DA8E652E07434BF7F1CEA7301C39700A8EA780C82007B4E0C60CD9D3FF",
                    "C3B3ADDC4F7BF4EBC73C48D28292587BE2EE47947822367A8A1281F815A0F847",
                    "13C160E7C6EA743B25BF06478A42DE84D75A2C64ECD97B8C776101EF92D84FE5",
                    "DE85824522D0A973380A07A319A42134BE45DF04964D84DFADB5AB592D2437C2",
                    "F6F3AAD772A7E35F5528366C67DA208CB757DC6D78F2885F4A5E69D85CC08FF6",
                    "858001E3BE67F0CD3D040C54D771AFA375D90C4B8AEE0F4A495BA45C62965EE1",
                    "7298E2B226A6C5A33B62EDC7471B3D36B3446FC11ED075AA448260EAEB0CC498",
                    "DF2960F666683BB9B3096EB8557F677AA294DC603D7FF8688908A875160CF847",
                    "0CDE1BEA4E90AF968B177ACB9B364B0EF91B9E0467A267590F55DADBF4DF02D5",
                    "022036FB971D2A95B7D0AFEC585F9E0E5F2C047001D7F23742440C1929BB8166",
                    "EF32A34D4ABD110F06E26182600980AEBD33D53C8FCDFFA270FAA7BD9BDB95E6",
                    "8A97D3292D65478B255E8EE62BCD5DF27C227C5CE9DD3F770554FEE1E9E3B75E",
                    "87F71E2BACC59AB258C2509EAA13457828BD8720215D64003A03E14E28F902CD",
                    "10129BC0D3F04498AA9C1407A095DCAF78F186DBAC583EE47E4716EBF67E34A6",
                    "20AC9573831741854B461F6737E48CBA99C586D20559F52DFCCB5A915E1A96CB",
                    "6EAACBAF71BB5F0352C49471C9B613ECDE29C010F11ABFDFAAAA4939163DCF58",
                    "37DD231C0DCC4F65FBAC7D2D2088AB93CFAA3F43C6826B0152B47DF701D63080",
                    "426ED0D26E55361F0503AD2218BED09BFA4C555C44DE53AC264DA6D2D798D72B",
                    "D389D4E55C7DC8B9085380EEA21487732A85938EC02A574649E4476558457EE2",
                    "BAF94B392FC6692F830727E206CCA430B82C641586F612343482A791E568660A",
                    "65F1A041CE057315D3BEAA437C44B79F3462E4556AFF0DA138824FC00C6EDED4",
                };
                int index = 0;
                for (int profile = 0; profile < 3; profile++)
                {
                    var settings = profile == 0 ? AppSettings.Defaults() : Profile(profile == 1 ? 0 : 2);
                    foreach (string text in texts)
                    {
                        foreach (int seed in new[] { 0, 17, 791 })
                        {
                            string log = RunEngine(text, settings, seed, Int64.MaxValue, false).Log();
                            using (var hash = SHA256.Create())
                            {
                                string actual = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(log))).Replace("-", "");
                                Assert(actual == expected[index], "engine golden trace " + index + " seed=" + seed);
                            }
                            index++;
                        }
                    }
                }
            });
        }
    }
}
