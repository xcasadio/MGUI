using System.Globalization;
using System.Text;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Architecture;

/// <summary>
/// T1.3 of the plan "end of the zero allocation of the drawing": the triangle of <see cref="MGRatingControl"/> was drawn from a list of scaled vertices that was built with LINQ at each
/// draw, its stroke from <c>SelectConsecutivePairs(true)</c>, and the bounds of its partial clip from <c>Min</c> and <c>Max</c> lambdas. It is now drawn from a buffer that is kept (the one of the
/// star and the diamond), by index, and must make exactly the same calls, with the same arguments, in the same order.<para/>
/// <see cref="Frozen"/> is the SHA-256 (first 16 hex digits) and the line count of the whole journal of each scene, written by an <see cref="OrderedRecordingDrawTransaction"/> (floats at their
/// round-trip form, so a vertex is compared bit for bit), frozen on the code that used LINQ before it was changed. The scenes cross the orientation, the value (empty, filled, and several partial
/// fractions: one that makes the clip 0 pixels wide or high, ones very close to 1), the thickness of the stroke (0, positive, negative) of the unfilled, filled and preview drawings, the size of
/// the items (odd sizes, 1, 0 and a negative one), the number of items, the window scale. The thickness of the stroke of a rating control is an integer: a fractional one cannot be given.<para/>
/// <see cref="TheTriangleJournal_MatchesTheOldLinqExpressions"/> checks, in every scene and independently of the frozen text, the vertices and the order of the line segments against the
/// literal old expressions.
/// </summary>
public class RatingTriangleJournalTests
{
    private sealed record Scene(string Name, Orientation Orientation, float Value, int UnfilledStroke, int FilledStroke, int PreviewStroke, float? Preview, int ItemSize, int Items, float WindowScale);

    private static string F(float value) => value.ToString(CultureInfo.InvariantCulture);

    private static Scene Make(Orientation orientation, float value, int unfilled, int filled, int itemSize = 24, int items = 4, float? preview = null, int previewStroke = 0, float scale = 1f)
        => new($"{(orientation == Orientation.Horizontal ? "H" : "V")} v={F(value)} s={unfilled}/{filled}/{previewStroke} size={itemSize} n={items} p={(preview.HasValue ? F(preview.Value) : "-")} w={F(scale)}",
            orientation, value, unfilled, filled, previewStroke, preview, itemSize, items, scale);

    private static readonly List<Scene> AllScenes = BuildScenes();

    private static List<Scene> BuildScenes()
    {
        List<Scene> scenes = new();
        float[] values = { 0f, 0.0004f, 0.3f, 0.5f, 0.9999f, 0.9999998f, 2.4f, 4f };
        (int Unfilled, int Filled)[] strokes = { (0, 0), (1, 1), (2, 2), (3, 1), (1, 0), (0, 2), (-1, 2), (2, -3) };
        foreach (Orientation orientation in new[] { Orientation.Horizontal, Orientation.Vertical })
        {
            foreach (float value in values)
            {
                foreach ((int unfilled, int filled) in strokes)
                {
                    scenes.Add(Make(orientation, value, unfilled, filled));
                }
            }

            foreach (int size in new[] { 0, 1, 2, 3, 5, 7, 25, 64, 101, 256, 257, -24 })
            {
                foreach (float value in new[] { 2.4f, 0.0004f, 0.9999998f, 3.5f })
                {
                    scenes.Add(Make(orientation, value, 1, 2, size));
                    scenes.Add(Make(orientation, value, 0, 0, size));
                }
            }

            foreach (int items in new[] { 1, 2, 7 })
            {
                scenes.Add(Make(orientation, items - 0.6f, 1, 2, 24, items));
                scenes.Add(Make(orientation, items, 2, 1, 24, items));
            }

            foreach (float scale in new[] { 0.5f, 1.5f, 2f })
            {
                scenes.Add(Make(orientation, 2.4f, 1, 2, 24, 4, null, 0, scale));
                scenes.Add(Make(orientation, 1.7f, 0, 1, 33, 4, null, 0, scale));
            }

            foreach (float preview in new[] { 0f, 1.5f, 3.25f, 4f, 0.0004f })
            {
                scenes.Add(Make(orientation, 2.4f, 1, 2, 24, 4, preview, 0));
                scenes.Add(Make(orientation, 2.4f, 1, 2, 24, 4, preview, 2));
                scenes.Add(Make(orientation, 0f, 0, 0, 17, 4, preview, 3));
            }
        }

        return scenes;
    }

    public static IEnumerable<object[]> SceneNames()
    {
        foreach (Scene scene in AllScenes)
        {
            yield return new object[] { scene.Name };
        }
    }

    private static List<string> Draw(Scene scene)
    {
        Rectangle surface = new(0, 0, 3000, 3000);
        GraphTestRuntime runtime = new(surface);
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 20, 30, 2800, 2800) { WindowStyle = WindowStyle.None };
        MGRatingControl rating = new(window, 0, scene.Items, scene.ItemSize, false, null, RatingItemShape.Triangle) { Orientation = scene.Orientation };
        rating.UnfilledShapeStrokeThickness = scene.UnfilledStroke;
        rating.FilledShapeStrokeThickness = scene.FilledStroke;
        rating.PreviewShapeStrokeThickness = scene.PreviewStroke;
        rating.SetValue(scene.Value);
        window.SetContent(rating);
        desktop.Windows.Add(window);
        window.Scale = scene.WindowScale;
        if (scene.Preview.HasValue)
        {
            typeof(MGRatingControl).GetProperty(nameof(MGRatingControl.PreviewValue))!.GetSetMethod(true)!.Invoke(rating, new object[] { scene.Preview });
        }

        for (int i = 1; i <= 2; i++)
        {
            runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * i), TimeSpan.FromMilliseconds(16), default, new KeyboardState()));
            desktop.Update();
        }

        OrderedRecordingDrawTransaction transaction = new(runtime, DrawSettings.Default);
        desktop.Draw(transaction);
        return transaction.Lines.ToList();
    }

    private static Scene Find(string name) => AllScenes.Single(x => x.Name == name);

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void TheTriangleJournal_IsTheOneOfTheLinqCode(string name)
    {
        List<string> lines = Draw(Find(name));
        string capturePath = Environment.GetEnvironmentVariable("RATING_TRIANGLE_CAPTURE");
        if (!string.IsNullOrEmpty(capturePath))
        {
            lock (typeof(RatingTriangleJournalTests))
            {
                System.IO.File.AppendAllText(capturePath, $"{name}\t{lines.Count}\t{FrozenJournal.Hash(string.Join('\n', lines))}\n");
            }

            return;
        }

        Assert.True(Frozen.TryGetValue(name, out (int Lines, string Hash) expected), $"no frozen journal for {name}");
        FrozenJournal.Assert(name, expected.Lines, expected.Hash, lines);
    }

    //  The old expressions, copied literally (see git history of MGRatingControl.DrawValue): the vertices of the triangle at 256, scaled with Select, the pairs of SelectConsecutivePairs(true).
    private static readonly Vector2[] Triangle256 = { new(0, 256 - 256 / 8), new(256, 256 - 256 / 8), new(128, 0 + 256 / 8) };

    private static string Vec(Vector2 v) => $"({F(v.X)},{F(v.Y)})";

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void TheTriangleJournal_MatchesTheOldLinqExpressions(string name)
    {
        Scene scene = Find(name);
        List<string> lines = Draw(scene);
        float scaleFactor = scene.ItemSize / 256.0f;
        IList<Vector2> old = Triangle256.Select(x => x * scaleFactor).ToList();
        List<(Vector2, Vector2)> edges = GeneralUtils.SelectConsecutivePairs(old, true).ToList();

        int triangles = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            string[] parts = lines[i].Split(' ');
            if (parts[0] == "FillTriangle/7")
            {
                triangles++;
                Assert.Equal(Vec(old[0]), parts[2]);
                Assert.Equal(Vec(old[1]), parts[4]);
                Assert.Equal(Vec(old[2]), parts[6]);
                //  The stroke follows the fill: three edges in the order of the pairs, or none (nothing else is drawn between a fill and its stroke).
                int next = i + 1;
                int k = 0;
                while (next < lines.Count && lines[next].StartsWith("StrokeLineSegment/5 ", StringComparison.Ordinal))
                {
                    string[] s = lines[next].Split(' ');
                    Assert.Equal(parts[1], s[1]);
                    Assert.Equal(Vec(edges[k].Item1), s[2]);
                    Assert.Equal(Vec(edges[k].Item2), s[3]);
                    k++;
                    next++;
                }

                Assert.True(k == 0 || k == 3, $"{name}: {k} line segments after a triangle");
            }
        }

        //  Every scene draws at least the four unfilled triangles of its items.
        Assert.True(triangles >= scene.Items || scene.ItemSize <= 0, $"{name}: {triangles} triangles");
    }

    private static readonly Dictionary<string, (int Lines, string Hash)> Frozen = new()
    {
        ["H v=0 s=-1/2/0 size=24 n=4 p=- w=1"] = (14, "E4F2D4A6AFE15EF7"),
        ["H v=0 s=0/0/0 size=24 n=4 p=- w=1"] = (14, "E4F2D4A6AFE15EF7"),
        ["H v=0 s=0/0/3 size=17 n=4 p=0 w=1"] = (14, "4415240467F16F0D"),
        ["H v=0 s=0/0/3 size=17 n=4 p=0.0004 w=1"] = (20, "227314D39C78A0F7"),
        ["H v=0 s=0/0/3 size=17 n=4 p=1.5 w=1"] = (24, "6583691A1AC0C729"),
        ["H v=0 s=0/0/3 size=17 n=4 p=3.25 w=1"] = (32, "3DAC8A726CF3598A"),
        ["H v=0 s=0/0/3 size=17 n=4 p=4 w=1"] = (30, "153A8DB5528CA2CC"),
        ["H v=0 s=0/2/0 size=24 n=4 p=- w=1"] = (14, "E4F2D4A6AFE15EF7"),
        ["H v=0 s=1/0/0 size=24 n=4 p=- w=1"] = (26, "F2ADA4519E4EF102"),
        ["H v=0 s=1/1/0 size=24 n=4 p=- w=1"] = (26, "F2ADA4519E4EF102"),
        ["H v=0 s=2/-3/0 size=24 n=4 p=- w=1"] = (26, "681D2917FBC82F6D"),
        ["H v=0 s=2/2/0 size=24 n=4 p=- w=1"] = (26, "681D2917FBC82F6D"),
        ["H v=0 s=3/1/0 size=24 n=4 p=- w=1"] = (26, "B5DA88E338BBE274"),
        ["H v=0.0004 s=-1/2/0 size=24 n=4 p=- w=1"] = (20, "9F5CD4AFC3A5AF04"),
        ["H v=0.0004 s=0/0/0 size=-24 n=4 p=- w=1"] = (17, "89B4B73E2E0D6D57"),
        ["H v=0.0004 s=0/0/0 size=0 n=4 p=- w=1"] = (17, "D04CB58D8C5EA0C0"),
        ["H v=0.0004 s=0/0/0 size=1 n=4 p=- w=1"] = (17, "1AA9FA551B031D78"),
        ["H v=0.0004 s=0/0/0 size=101 n=4 p=- w=1"] = (17, "50D1655F8572181C"),
        ["H v=0.0004 s=0/0/0 size=2 n=4 p=- w=1"] = (17, "B84BDBDEF5617707"),
        ["H v=0.0004 s=0/0/0 size=24 n=4 p=- w=1"] = (17, "F5BCF88C002BEB18"),
        ["H v=0.0004 s=0/0/0 size=25 n=4 p=- w=1"] = (17, "FC228E8DAC8BB0D5"),
        ["H v=0.0004 s=0/0/0 size=256 n=4 p=- w=1"] = (17, "8E37F2BE41113C11"),
        ["H v=0.0004 s=0/0/0 size=257 n=4 p=- w=1"] = (17, "7EEDA396D68BFC98"),
        ["H v=0.0004 s=0/0/0 size=3 n=4 p=- w=1"] = (17, "92F006335EBACDDC"),
        ["H v=0.0004 s=0/0/0 size=5 n=4 p=- w=1"] = (17, "9C70A33B6AAFBD75"),
        ["H v=0.0004 s=0/0/0 size=64 n=4 p=- w=1"] = (17, "2DCE57E8336EC525"),
        ["H v=0.0004 s=0/0/0 size=7 n=4 p=- w=1"] = (17, "C7439D040FF9444B"),
        ["H v=0.0004 s=0/2/0 size=24 n=4 p=- w=1"] = (20, "9F5CD4AFC3A5AF04"),
        ["H v=0.0004 s=1/0/0 size=24 n=4 p=- w=1"] = (29, "80F9B2DCA5EF2C44"),
        ["H v=0.0004 s=1/1/0 size=24 n=4 p=- w=1"] = (32, "03DC1F73F6A07069"),
        ["H v=0.0004 s=1/2/0 size=-24 n=4 p=- w=1"] = (32, "C6E387D08114DF5B"),
        ["H v=0.0004 s=1/2/0 size=0 n=4 p=- w=1"] = (32, "2B025C1CFBEA15B7"),
        ["H v=0.0004 s=1/2/0 size=1 n=4 p=- w=1"] = (32, "0837D0C9A3D5FC45"),
        ["H v=0.0004 s=1/2/0 size=101 n=4 p=- w=1"] = (32, "39D7A81A29C14FAB"),
        ["H v=0.0004 s=1/2/0 size=2 n=4 p=- w=1"] = (32, "F1B98B709AD24E48"),
        ["H v=0.0004 s=1/2/0 size=25 n=4 p=- w=1"] = (32, "8B0C13016F8C6262"),
        ["H v=0.0004 s=1/2/0 size=256 n=4 p=- w=1"] = (32, "883870919DDC13A1"),
        ["H v=0.0004 s=1/2/0 size=257 n=4 p=- w=1"] = (32, "116F45F660431ABB"),
        ["H v=0.0004 s=1/2/0 size=3 n=4 p=- w=1"] = (32, "4D528F5BE6B95D85"),
        ["H v=0.0004 s=1/2/0 size=5 n=4 p=- w=1"] = (32, "47530255F699E55E"),
        ["H v=0.0004 s=1/2/0 size=64 n=4 p=- w=1"] = (32, "A9013D9AB4117A92"),
        ["H v=0.0004 s=1/2/0 size=7 n=4 p=- w=1"] = (32, "AE2271E8DD076C25"),
        ["H v=0.0004 s=2/-3/0 size=24 n=4 p=- w=1"] = (29, "21CC2DD4E32EA53F"),
        ["H v=0.0004 s=2/2/0 size=24 n=4 p=- w=1"] = (32, "9180AF65F80FD1C0"),
        ["H v=0.0004 s=3/1/0 size=24 n=4 p=- w=1"] = (32, "CBA3A1E6A82E941E"),
        ["H v=0.3 s=-1/2/0 size=24 n=4 p=- w=1"] = (20, "5D07B8B6B38294FD"),
        ["H v=0.3 s=0/0/0 size=24 n=4 p=- w=1"] = (17, "F2C67F600789CAA2"),
        ["H v=0.3 s=0/2/0 size=24 n=4 p=- w=1"] = (20, "5D07B8B6B38294FD"),
        ["H v=0.3 s=1/0/0 size=24 n=4 p=- w=1"] = (29, "58C52BF7FB8F782F"),
        ["H v=0.3 s=1/1/0 size=24 n=4 p=- w=1"] = (32, "0C69AFC5164FAE1E"),
        ["H v=0.3 s=2/-3/0 size=24 n=4 p=- w=1"] = (29, "570D1BDAC83CEE0C"),
        ["H v=0.3 s=2/2/0 size=24 n=4 p=- w=1"] = (32, "0FAC334D9D806C63"),
        ["H v=0.3 s=3/1/0 size=24 n=4 p=- w=1"] = (32, "F5FC9B38D31AA8EF"),
        ["H v=0.39999998 s=1/2/0 size=24 n=1 p=- w=1"] = (20, "29BF3B4D8DF6C536"),
        ["H v=0.5 s=-1/2/0 size=24 n=4 p=- w=1"] = (20, "129D68500733B586"),
        ["H v=0.5 s=0/0/0 size=24 n=4 p=- w=1"] = (17, "38DA20DCBFD9AEEC"),
        ["H v=0.5 s=0/2/0 size=24 n=4 p=- w=1"] = (20, "129D68500733B586"),
        ["H v=0.5 s=1/0/0 size=24 n=4 p=- w=1"] = (29, "A5A865C6C914A2F1"),
        ["H v=0.5 s=1/1/0 size=24 n=4 p=- w=1"] = (32, "6C13A1149093DEB9"),
        ["H v=0.5 s=2/-3/0 size=24 n=4 p=- w=1"] = (29, "A2F8B2911F49C5AF"),
        ["H v=0.5 s=2/2/0 size=24 n=4 p=- w=1"] = (32, "4D85AF3454CC53B5"),
        ["H v=0.5 s=3/1/0 size=24 n=4 p=- w=1"] = (32, "F701A28419D8D038"),
        ["H v=0.9999 s=-1/2/0 size=24 n=4 p=- w=1"] = (20, "A8F0382E2DC6C4E5"),
        ["H v=0.9999 s=0/0/0 size=24 n=4 p=- w=1"] = (17, "09E1942778204E1A"),
        ["H v=0.9999 s=0/2/0 size=24 n=4 p=- w=1"] = (20, "A8F0382E2DC6C4E5"),
        ["H v=0.9999 s=1/0/0 size=24 n=4 p=- w=1"] = (29, "99A6DA0D2B0C35E4"),
        ["H v=0.9999 s=1/1/0 size=24 n=4 p=- w=1"] = (32, "A1BACCEBB2A325A4"),
        ["H v=0.9999 s=2/-3/0 size=24 n=4 p=- w=1"] = (29, "97846AD8423E505E"),
        ["H v=0.9999 s=2/2/0 size=24 n=4 p=- w=1"] = (32, "B472C29F4730ACC7"),
        ["H v=0.9999 s=3/1/0 size=24 n=4 p=- w=1"] = (32, "754B1048DB085EF2"),
        ["H v=0.9999998 s=-1/2/0 size=24 n=4 p=- w=1"] = (20, "A8F0382E2DC6C4E5"),
        ["H v=0.9999998 s=0/0/0 size=-24 n=4 p=- w=1"] = (17, "C12D320F11AA11AB"),
        ["H v=0.9999998 s=0/0/0 size=0 n=4 p=- w=1"] = (17, "D04CB58D8C5EA0C0"),
        ["H v=0.9999998 s=0/0/0 size=1 n=4 p=- w=1"] = (17, "1AA9FA551B031D78"),
        ["H v=0.9999998 s=0/0/0 size=101 n=4 p=- w=1"] = (17, "845799ADD3A52257"),
        ["H v=0.9999998 s=0/0/0 size=2 n=4 p=- w=1"] = (17, "D289ABEDE459A490"),
        ["H v=0.9999998 s=0/0/0 size=24 n=4 p=- w=1"] = (17, "09E1942778204E1A"),
        ["H v=0.9999998 s=0/0/0 size=25 n=4 p=- w=1"] = (17, "074D13600724D1EF"),
        ["H v=0.9999998 s=0/0/0 size=256 n=4 p=- w=1"] = (17, "AAEFC13BE3FF352D"),
        ["H v=0.9999998 s=0/0/0 size=257 n=4 p=- w=1"] = (17, "1F4D265C31F62ED2"),
        ["H v=0.9999998 s=0/0/0 size=3 n=4 p=- w=1"] = (17, "872AD3FCEA3A3622"),
        ["H v=0.9999998 s=0/0/0 size=5 n=4 p=- w=1"] = (17, "CF5915FCF8DB833B"),
        ["H v=0.9999998 s=0/0/0 size=64 n=4 p=- w=1"] = (17, "E2E323ADDFAD7D38"),
        ["H v=0.9999998 s=0/0/0 size=7 n=4 p=- w=1"] = (17, "294513B08B7F4D2C"),
        ["H v=0.9999998 s=0/2/0 size=24 n=4 p=- w=1"] = (20, "A8F0382E2DC6C4E5"),
        ["H v=0.9999998 s=1/0/0 size=24 n=4 p=- w=1"] = (29, "99A6DA0D2B0C35E4"),
        ["H v=0.9999998 s=1/1/0 size=24 n=4 p=- w=1"] = (32, "A1BACCEBB2A325A4"),
        ["H v=0.9999998 s=1/2/0 size=-24 n=4 p=- w=1"] = (32, "D9D9C216532437B4"),
        ["H v=0.9999998 s=1/2/0 size=0 n=4 p=- w=1"] = (32, "2B025C1CFBEA15B7"),
        ["H v=0.9999998 s=1/2/0 size=1 n=4 p=- w=1"] = (32, "0837D0C9A3D5FC45"),
        ["H v=0.9999998 s=1/2/0 size=101 n=4 p=- w=1"] = (32, "EBF7E779ACBD789E"),
        ["H v=0.9999998 s=1/2/0 size=2 n=4 p=- w=1"] = (32, "4FD71CFF0F3FA285"),
        ["H v=0.9999998 s=1/2/0 size=25 n=4 p=- w=1"] = (32, "1CFB9F702FA89345"),
        ["H v=0.9999998 s=1/2/0 size=256 n=4 p=- w=1"] = (32, "3091A1AB0D2CE5CA"),
        ["H v=0.9999998 s=1/2/0 size=257 n=4 p=- w=1"] = (32, "7F86FD2D57DB2EAA"),
        ["H v=0.9999998 s=1/2/0 size=3 n=4 p=- w=1"] = (32, "EB2BABAD38B80FEE"),
        ["H v=0.9999998 s=1/2/0 size=5 n=4 p=- w=1"] = (32, "985483A84C0D9057"),
        ["H v=0.9999998 s=1/2/0 size=64 n=4 p=- w=1"] = (32, "92EE2D8E9D4E5EF5"),
        ["H v=0.9999998 s=1/2/0 size=7 n=4 p=- w=1"] = (32, "24BA3612CCACEE60"),
        ["H v=0.9999998 s=2/-3/0 size=24 n=4 p=- w=1"] = (29, "97846AD8423E505E"),
        ["H v=0.9999998 s=2/2/0 size=24 n=4 p=- w=1"] = (32, "B472C29F4730ACC7"),
        ["H v=0.9999998 s=3/1/0 size=24 n=4 p=- w=1"] = (32, "754B1048DB085EF2"),
        ["H v=1 s=2/1/0 size=24 n=1 p=- w=1"] = (18, "74DA8C7695E06B7B"),
        ["H v=1.4 s=1/2/0 size=24 n=2 p=- w=1"] = (28, "0229FBB73BC27725"),
        ["H v=1.7 s=0/1/0 size=33 n=4 p=- w=0.5"] = (26, "85990E798E08F6DB"),
        ["H v=1.7 s=0/1/0 size=33 n=4 p=- w=1.5"] = (26, "FDCC818F8CEF69C5"),
        ["H v=1.7 s=0/1/0 size=33 n=4 p=- w=2"] = (26, "B69FAEF406C829E2"),
        ["H v=2 s=2/1/0 size=24 n=2 p=- w=1"] = (26, "452EE59ADCEFD303"),
        ["H v=2.4 s=-1/2/0 size=24 n=4 p=- w=1"] = (28, "5434AEB2AC9D2488"),
        ["H v=2.4 s=0/0/0 size=-24 n=4 p=- w=1"] = (19, "7F87FAE5A9ED9AE8"),
        ["H v=2.4 s=0/0/0 size=0 n=4 p=- w=1"] = (19, "FC9ED9158D4CE18E"),
        ["H v=2.4 s=0/0/0 size=1 n=4 p=- w=1"] = (19, "3F6B6CFD7602854D"),
        ["H v=2.4 s=0/0/0 size=101 n=4 p=- w=1"] = (19, "4B6E3D8239FC650D"),
        ["H v=2.4 s=0/0/0 size=2 n=4 p=- w=1"] = (19, "F74F9F1585147B45"),
        ["H v=2.4 s=0/0/0 size=24 n=4 p=- w=1"] = (19, "9FE810F9FD05F097"),
        ["H v=2.4 s=0/0/0 size=25 n=4 p=- w=1"] = (19, "8948D4757F882806"),
        ["H v=2.4 s=0/0/0 size=256 n=4 p=- w=1"] = (19, "FC35F5816D89914A"),
        ["H v=2.4 s=0/0/0 size=257 n=4 p=- w=1"] = (19, "CFF04177F7FAA33D"),
        ["H v=2.4 s=0/0/0 size=3 n=4 p=- w=1"] = (19, "E3FE6FEA2E6C4933"),
        ["H v=2.4 s=0/0/0 size=5 n=4 p=- w=1"] = (19, "E94B4F2B3DE1296E"),
        ["H v=2.4 s=0/0/0 size=64 n=4 p=- w=1"] = (19, "1856E2B30BFCCD96"),
        ["H v=2.4 s=0/0/0 size=7 n=4 p=- w=1"] = (19, "B0F0921254A9A683"),
        ["H v=2.4 s=0/2/0 size=24 n=4 p=- w=1"] = (28, "5434AEB2AC9D2488"),
        ["H v=2.4 s=1/0/0 size=24 n=4 p=- w=1"] = (31, "EFCEDED138380E5D"),
        ["H v=2.4 s=1/1/0 size=24 n=4 p=- w=1"] = (40, "FC1DF13D073F970A"),
        ["H v=2.4 s=1/2/0 size=-24 n=4 p=- w=1"] = (40, "D9B5269EA1B1D671"),
        ["H v=2.4 s=1/2/0 size=0 n=4 p=- w=1"] = (40, "72828AEC6B25E7EA"),
        ["H v=2.4 s=1/2/0 size=1 n=4 p=- w=1"] = (40, "3E7FE8A3B9D389AB"),
        ["H v=2.4 s=1/2/0 size=101 n=4 p=- w=1"] = (40, "537691AE634A84F3"),
        ["H v=2.4 s=1/2/0 size=2 n=4 p=- w=1"] = (40, "E8F5C55542C82647"),
        ["H v=2.4 s=1/2/0 size=24 n=4 p=- w=0.5"] = (42, "095E4E6B473D4DC8"),
        ["H v=2.4 s=1/2/0 size=24 n=4 p=- w=1.5"] = (42, "84FD08CD3DC48B33"),
        ["H v=2.4 s=1/2/0 size=24 n=4 p=- w=2"] = (42, "411171BD6DFDF79C"),
        ["H v=2.4 s=1/2/0 size=24 n=4 p=0 w=1"] = (40, "BBBEB25684AD9AEC"),
        ["H v=2.4 s=1/2/0 size=24 n=4 p=0.0004 w=1"] = (43, "8AA80680CB55AD50"),
        ["H v=2.4 s=1/2/0 size=24 n=4 p=1.5 w=1"] = (44, "0A5FCA7B530C7889"),
        ["H v=2.4 s=1/2/0 size=24 n=4 p=3.25 w=1"] = (46, "E0D37AE051671B99"),
        ["H v=2.4 s=1/2/0 size=24 n=4 p=4 w=1"] = (44, "7CCC18B1CD933672"),
        ["H v=2.4 s=1/2/0 size=25 n=4 p=- w=1"] = (40, "39262C182CF4A6F9"),
        ["H v=2.4 s=1/2/0 size=256 n=4 p=- w=1"] = (40, "B6CEBBC019010A85"),
        ["H v=2.4 s=1/2/0 size=257 n=4 p=- w=1"] = (40, "839AC67553F5AB6F"),
        ["H v=2.4 s=1/2/0 size=3 n=4 p=- w=1"] = (40, "F756FEFB5B22A7AA"),
        ["H v=2.4 s=1/2/0 size=5 n=4 p=- w=1"] = (40, "DF6AD9CA7577ED55"),
        ["H v=2.4 s=1/2/0 size=64 n=4 p=- w=1"] = (40, "3D536F66904A8F02"),
        ["H v=2.4 s=1/2/0 size=7 n=4 p=- w=1"] = (40, "964B0701B70AB070"),
        ["H v=2.4 s=1/2/2 size=24 n=4 p=0 w=1"] = (40, "BBBEB25684AD9AEC"),
        ["H v=2.4 s=1/2/2 size=24 n=4 p=0.0004 w=1"] = (46, "B0E72B9CC62EBBBF"),
        ["H v=2.4 s=1/2/2 size=24 n=4 p=1.5 w=1"] = (50, "4953AB1CF323A312"),
        ["H v=2.4 s=1/2/2 size=24 n=4 p=3.25 w=1"] = (58, "2CF71CC1ABA2CA29"),
        ["H v=2.4 s=1/2/2 size=24 n=4 p=4 w=1"] = (56, "A7164A1202295111"),
        ["H v=2.4 s=2/-3/0 size=24 n=4 p=- w=1"] = (31, "A2C686D86F884FC1"),
        ["H v=2.4 s=2/2/0 size=24 n=4 p=- w=1"] = (40, "E1414EF3FFF877C2"),
        ["H v=2.4 s=3/1/0 size=24 n=4 p=- w=1"] = (40, "2E09A28B22FA7F84"),
        ["H v=3.5 s=0/0/0 size=-24 n=4 p=- w=1"] = (20, "35514D9493B963ED"),
        ["H v=3.5 s=0/0/0 size=0 n=4 p=- w=1"] = (20, "28471AFA15A34068"),
        ["H v=3.5 s=0/0/0 size=1 n=4 p=- w=1"] = (20, "2ECA503F7A7DB6F5"),
        ["H v=3.5 s=0/0/0 size=101 n=4 p=- w=1"] = (20, "97099D78D5B3BAFE"),
        ["H v=3.5 s=0/0/0 size=2 n=4 p=- w=1"] = (20, "07B70C236086E781"),
        ["H v=3.5 s=0/0/0 size=25 n=4 p=- w=1"] = (20, "A0EB37EAF9C1A5A3"),
        ["H v=3.5 s=0/0/0 size=256 n=4 p=- w=1"] = (20, "8A37BDB8AC6BE798"),
        ["H v=3.5 s=0/0/0 size=257 n=4 p=- w=1"] = (20, "416B1C0A2A89541C"),
        ["H v=3.5 s=0/0/0 size=3 n=4 p=- w=1"] = (20, "46D6A612158674E7"),
        ["H v=3.5 s=0/0/0 size=5 n=4 p=- w=1"] = (20, "084A4DFA4A5121B3"),
        ["H v=3.5 s=0/0/0 size=64 n=4 p=- w=1"] = (20, "25C0CD85D4EB2D3A"),
        ["H v=3.5 s=0/0/0 size=7 n=4 p=- w=1"] = (20, "557BC52AEC01841F"),
        ["H v=3.5 s=1/2/0 size=-24 n=4 p=- w=1"] = (44, "CB6DAEB6D942D11C"),
        ["H v=3.5 s=1/2/0 size=0 n=4 p=- w=1"] = (44, "607DED368E4B3850"),
        ["H v=3.5 s=1/2/0 size=1 n=4 p=- w=1"] = (44, "4DB90DF295F3F75E"),
        ["H v=3.5 s=1/2/0 size=101 n=4 p=- w=1"] = (44, "838EEB0AAF106F41"),
        ["H v=3.5 s=1/2/0 size=2 n=4 p=- w=1"] = (44, "70CBC03662E96734"),
        ["H v=3.5 s=1/2/0 size=25 n=4 p=- w=1"] = (44, "FBD2B961985B5E2E"),
        ["H v=3.5 s=1/2/0 size=256 n=4 p=- w=1"] = (44, "98220C6CB12B835A"),
        ["H v=3.5 s=1/2/0 size=257 n=4 p=- w=1"] = (44, "4CC6FC601C6DFA6A"),
        ["H v=3.5 s=1/2/0 size=3 n=4 p=- w=1"] = (44, "55C86C0562A38672"),
        ["H v=3.5 s=1/2/0 size=5 n=4 p=- w=1"] = (44, "B1A351FF2BB6D4F3"),
        ["H v=3.5 s=1/2/0 size=64 n=4 p=- w=1"] = (44, "8B25B25CB402D52B"),
        ["H v=3.5 s=1/2/0 size=7 n=4 p=- w=1"] = (44, "994684AE943404CD"),
        ["H v=4 s=-1/2/0 size=24 n=4 p=- w=1"] = (30, "58D5FF94B20FAFD5"),
        ["H v=4 s=0/0/0 size=24 n=4 p=- w=1"] = (18, "3D5D749794CF4760"),
        ["H v=4 s=0/2/0 size=24 n=4 p=- w=1"] = (30, "58D5FF94B20FAFD5"),
        ["H v=4 s=1/0/0 size=24 n=4 p=- w=1"] = (30, "05FA82C797F1BDF7"),
        ["H v=4 s=1/1/0 size=24 n=4 p=- w=1"] = (42, "BB5028C2EA6913C1"),
        ["H v=4 s=2/-3/0 size=24 n=4 p=- w=1"] = (30, "E7E423EAF79674B8"),
        ["H v=4 s=2/2/0 size=24 n=4 p=- w=1"] = (42, "FE371FDD0561374E"),
        ["H v=4 s=3/1/0 size=24 n=4 p=- w=1"] = (42, "59DCE1EE46DB0BAB"),
        ["H v=6.4 s=1/2/0 size=24 n=7 p=- w=1"] = (68, "F2C4EA25BE8D05BB"),
        ["H v=7 s=2/1/0 size=24 n=7 p=- w=1"] = (66, "5DA402536EC06F85"),
        ["V v=0 s=-1/2/0 size=24 n=4 p=- w=1"] = (14, "84BB7CA9562FEDB5"),
        ["V v=0 s=0/0/0 size=24 n=4 p=- w=1"] = (14, "84BB7CA9562FEDB5"),
        ["V v=0 s=0/0/3 size=17 n=4 p=0 w=1"] = (14, "52C864C9B65572E7"),
        ["V v=0 s=0/0/3 size=17 n=4 p=0.0004 w=1"] = (20, "67FC99DD5DA0A350"),
        ["V v=0 s=0/0/3 size=17 n=4 p=1.5 w=1"] = (24, "409D08E440E0D790"),
        ["V v=0 s=0/0/3 size=17 n=4 p=3.25 w=1"] = (32, "005DB4213899F369"),
        ["V v=0 s=0/0/3 size=17 n=4 p=4 w=1"] = (30, "DB02A097EE070DFE"),
        ["V v=0 s=0/2/0 size=24 n=4 p=- w=1"] = (14, "84BB7CA9562FEDB5"),
        ["V v=0 s=1/0/0 size=24 n=4 p=- w=1"] = (26, "4E89D1EF85E3A116"),
        ["V v=0 s=1/1/0 size=24 n=4 p=- w=1"] = (26, "4E89D1EF85E3A116"),
        ["V v=0 s=2/-3/0 size=24 n=4 p=- w=1"] = (26, "6D01D5BCF23B053C"),
        ["V v=0 s=2/2/0 size=24 n=4 p=- w=1"] = (26, "6D01D5BCF23B053C"),
        ["V v=0 s=3/1/0 size=24 n=4 p=- w=1"] = (26, "303BF4D9789980D1"),
        ["V v=0.0004 s=-1/2/0 size=24 n=4 p=- w=1"] = (20, "1AB2445A8FF07B7B"),
        ["V v=0.0004 s=0/0/0 size=-24 n=4 p=- w=1"] = (17, "02238645BD02E8EE"),
        ["V v=0.0004 s=0/0/0 size=0 n=4 p=- w=1"] = (17, "0FC7D7FC3AF1F4CB"),
        ["V v=0.0004 s=0/0/0 size=1 n=4 p=- w=1"] = (17, "88806563E21BF638"),
        ["V v=0.0004 s=0/0/0 size=101 n=4 p=- w=1"] = (17, "4E65108CC9A6AD84"),
        ["V v=0.0004 s=0/0/0 size=2 n=4 p=- w=1"] = (17, "993F6A03210190DC"),
        ["V v=0.0004 s=0/0/0 size=24 n=4 p=- w=1"] = (17, "69C2A28C2FDD69D7"),
        ["V v=0.0004 s=0/0/0 size=25 n=4 p=- w=1"] = (17, "668316EE73D5DF55"),
        ["V v=0.0004 s=0/0/0 size=256 n=4 p=- w=1"] = (17, "523C200F3939341B"),
        ["V v=0.0004 s=0/0/0 size=257 n=4 p=- w=1"] = (17, "80BB966F224ECE57"),
        ["V v=0.0004 s=0/0/0 size=3 n=4 p=- w=1"] = (17, "671DDAA8DECDF2F2"),
        ["V v=0.0004 s=0/0/0 size=5 n=4 p=- w=1"] = (17, "18386B8AD27940C6"),
        ["V v=0.0004 s=0/0/0 size=64 n=4 p=- w=1"] = (17, "A3C29E21CEC60EF6"),
        ["V v=0.0004 s=0/0/0 size=7 n=4 p=- w=1"] = (17, "8E5BFD2B5E5B43DB"),
        ["V v=0.0004 s=0/2/0 size=24 n=4 p=- w=1"] = (20, "1AB2445A8FF07B7B"),
        ["V v=0.0004 s=1/0/0 size=24 n=4 p=- w=1"] = (29, "48E595B664F4808D"),
        ["V v=0.0004 s=1/1/0 size=24 n=4 p=- w=1"] = (32, "F3E82A1EF808917B"),
        ["V v=0.0004 s=1/2/0 size=-24 n=4 p=- w=1"] = (32, "63CE4540137BE2E1"),
        ["V v=0.0004 s=1/2/0 size=0 n=4 p=- w=1"] = (32, "6EBFDCB4932E37B4"),
        ["V v=0.0004 s=1/2/0 size=1 n=4 p=- w=1"] = (32, "620DBE6B5380F1AB"),
        ["V v=0.0004 s=1/2/0 size=101 n=4 p=- w=1"] = (32, "3D98E83A50ABC03B"),
        ["V v=0.0004 s=1/2/0 size=2 n=4 p=- w=1"] = (32, "C1F7B4E6BD315393"),
        ["V v=0.0004 s=1/2/0 size=25 n=4 p=- w=1"] = (32, "A566F7E8EFBA0BE8"),
        ["V v=0.0004 s=1/2/0 size=256 n=4 p=- w=1"] = (32, "AD1F07AF1D38CFF7"),
        ["V v=0.0004 s=1/2/0 size=257 n=4 p=- w=1"] = (32, "1EBFE0BC89309339"),
        ["V v=0.0004 s=1/2/0 size=3 n=4 p=- w=1"] = (32, "C8E1D6D82AC6243F"),
        ["V v=0.0004 s=1/2/0 size=5 n=4 p=- w=1"] = (32, "D142678B8C86559E"),
        ["V v=0.0004 s=1/2/0 size=64 n=4 p=- w=1"] = (32, "D53A2EC7F9C8572E"),
        ["V v=0.0004 s=1/2/0 size=7 n=4 p=- w=1"] = (32, "C3C9258DAEAB26E1"),
        ["V v=0.0004 s=2/-3/0 size=24 n=4 p=- w=1"] = (29, "2B43A1972517E8AF"),
        ["V v=0.0004 s=2/2/0 size=24 n=4 p=- w=1"] = (32, "242C8CF866701FAA"),
        ["V v=0.0004 s=3/1/0 size=24 n=4 p=- w=1"] = (32, "9276E6531438355C"),
        ["V v=0.3 s=-1/2/0 size=24 n=4 p=- w=1"] = (20, "F22607E73EC32BF3"),
        ["V v=0.3 s=0/0/0 size=24 n=4 p=- w=1"] = (17, "BB19F3C451C56D83"),
        ["V v=0.3 s=0/2/0 size=24 n=4 p=- w=1"] = (20, "F22607E73EC32BF3"),
        ["V v=0.3 s=1/0/0 size=24 n=4 p=- w=1"] = (29, "3137D2CA7CC440B4"),
        ["V v=0.3 s=1/1/0 size=24 n=4 p=- w=1"] = (32, "CDF3743A23C2D717"),
        ["V v=0.3 s=2/-3/0 size=24 n=4 p=- w=1"] = (29, "1E009DD32F78AC52"),
        ["V v=0.3 s=2/2/0 size=24 n=4 p=- w=1"] = (32, "77C5572E977493AB"),
        ["V v=0.3 s=3/1/0 size=24 n=4 p=- w=1"] = (32, "860A7BBC2D9DA4BA"),
        ["V v=0.39999998 s=1/2/0 size=24 n=1 p=- w=1"] = (20, "CBB23EE751B0B791"),
        ["V v=0.5 s=-1/2/0 size=24 n=4 p=- w=1"] = (20, "A2BCB6161CC65A2B"),
        ["V v=0.5 s=0/0/0 size=24 n=4 p=- w=1"] = (17, "D036C646BC2F9135"),
        ["V v=0.5 s=0/2/0 size=24 n=4 p=- w=1"] = (20, "A2BCB6161CC65A2B"),
        ["V v=0.5 s=1/0/0 size=24 n=4 p=- w=1"] = (29, "A59FFAC2FDDD3BCA"),
        ["V v=0.5 s=1/1/0 size=24 n=4 p=- w=1"] = (32, "CE4D857BD52351EA"),
        ["V v=0.5 s=2/-3/0 size=24 n=4 p=- w=1"] = (29, "A1DE24291CB22BC4"),
        ["V v=0.5 s=2/2/0 size=24 n=4 p=- w=1"] = (32, "CA46F9FD11760257"),
        ["V v=0.5 s=3/1/0 size=24 n=4 p=- w=1"] = (32, "8BDAD98909A09055"),
        ["V v=0.9999 s=-1/2/0 size=24 n=4 p=- w=1"] = (20, "9979F07CF8B9C083"),
        ["V v=0.9999 s=0/0/0 size=24 n=4 p=- w=1"] = (17, "2F270D2CB9129C52"),
        ["V v=0.9999 s=0/2/0 size=24 n=4 p=- w=1"] = (20, "9979F07CF8B9C083"),
        ["V v=0.9999 s=1/0/0 size=24 n=4 p=- w=1"] = (29, "9B8106E97BA4F251"),
        ["V v=0.9999 s=1/1/0 size=24 n=4 p=- w=1"] = (32, "F62763C704B02582"),
        ["V v=0.9999 s=2/-3/0 size=24 n=4 p=- w=1"] = (29, "3C2D2212951CC67B"),
        ["V v=0.9999 s=2/2/0 size=24 n=4 p=- w=1"] = (32, "6B852F650F987CBD"),
        ["V v=0.9999 s=3/1/0 size=24 n=4 p=- w=1"] = (32, "97495F0E0BE41603"),
        ["V v=0.9999998 s=-1/2/0 size=24 n=4 p=- w=1"] = (20, "9979F07CF8B9C083"),
        ["V v=0.9999998 s=0/0/0 size=-24 n=4 p=- w=1"] = (17, "93977330B69A1D40"),
        ["V v=0.9999998 s=0/0/0 size=0 n=4 p=- w=1"] = (17, "0FC7D7FC3AF1F4CB"),
        ["V v=0.9999998 s=0/0/0 size=1 n=4 p=- w=1"] = (17, "88806563E21BF638"),
        ["V v=0.9999998 s=0/0/0 size=101 n=4 p=- w=1"] = (17, "945A63D94141F303"),
        ["V v=0.9999998 s=0/0/0 size=2 n=4 p=- w=1"] = (17, "5211A5BB5AB44709"),
        ["V v=0.9999998 s=0/0/0 size=24 n=4 p=- w=1"] = (17, "2F270D2CB9129C52"),
        ["V v=0.9999998 s=0/0/0 size=25 n=4 p=- w=1"] = (17, "266B99EC67BD3E3F"),
        ["V v=0.9999998 s=0/0/0 size=256 n=4 p=- w=1"] = (17, "7300753D8831D8DE"),
        ["V v=0.9999998 s=0/0/0 size=257 n=4 p=- w=1"] = (17, "32AE84C6F9179B5E"),
        ["V v=0.9999998 s=0/0/0 size=3 n=4 p=- w=1"] = (17, "7F7A08F1B1FC2767"),
        ["V v=0.9999998 s=0/0/0 size=5 n=4 p=- w=1"] = (17, "FF272736EE18F4D2"),
        ["V v=0.9999998 s=0/0/0 size=64 n=4 p=- w=1"] = (17, "8C9A241E569F1914"),
        ["V v=0.9999998 s=0/0/0 size=7 n=4 p=- w=1"] = (17, "7E912E3F7E01E63D"),
        ["V v=0.9999998 s=0/2/0 size=24 n=4 p=- w=1"] = (20, "9979F07CF8B9C083"),
        ["V v=0.9999998 s=1/0/0 size=24 n=4 p=- w=1"] = (29, "9B8106E97BA4F251"),
        ["V v=0.9999998 s=1/1/0 size=24 n=4 p=- w=1"] = (32, "F62763C704B02582"),
        ["V v=0.9999998 s=1/2/0 size=-24 n=4 p=- w=1"] = (32, "D95D730E36BB7108"),
        ["V v=0.9999998 s=1/2/0 size=0 n=4 p=- w=1"] = (32, "6EBFDCB4932E37B4"),
        ["V v=0.9999998 s=1/2/0 size=1 n=4 p=- w=1"] = (32, "620DBE6B5380F1AB"),
        ["V v=0.9999998 s=1/2/0 size=101 n=4 p=- w=1"] = (32, "1087AFD6CB7E06A0"),
        ["V v=0.9999998 s=1/2/0 size=2 n=4 p=- w=1"] = (32, "1B51E2D911F364C8"),
        ["V v=0.9999998 s=1/2/0 size=25 n=4 p=- w=1"] = (32, "039CA1E6FCF3AA3A"),
        ["V v=0.9999998 s=1/2/0 size=256 n=4 p=- w=1"] = (32, "81256B12D6067FD4"),
        ["V v=0.9999998 s=1/2/0 size=257 n=4 p=- w=1"] = (32, "E38A0ED8579E7668"),
        ["V v=0.9999998 s=1/2/0 size=3 n=4 p=- w=1"] = (32, "F89DD2AF5F33D2C2"),
        ["V v=0.9999998 s=1/2/0 size=5 n=4 p=- w=1"] = (32, "58CA452B4A63F6D6"),
        ["V v=0.9999998 s=1/2/0 size=64 n=4 p=- w=1"] = (32, "EDCE7011D176E474"),
        ["V v=0.9999998 s=1/2/0 size=7 n=4 p=- w=1"] = (32, "B8DFC985E6FAF8E8"),
        ["V v=0.9999998 s=2/-3/0 size=24 n=4 p=- w=1"] = (29, "3C2D2212951CC67B"),
        ["V v=0.9999998 s=2/2/0 size=24 n=4 p=- w=1"] = (32, "6B852F650F987CBD"),
        ["V v=0.9999998 s=3/1/0 size=24 n=4 p=- w=1"] = (32, "97495F0E0BE41603"),
        ["V v=1 s=2/1/0 size=24 n=1 p=- w=1"] = (18, "74DA8C7695E06B7B"),
        ["V v=1.4 s=1/2/0 size=24 n=2 p=- w=1"] = (28, "1BFC6C3F7D5B6FE1"),
        ["V v=1.7 s=0/1/0 size=33 n=4 p=- w=0.5"] = (26, "09B79A8D61D5085D"),
        ["V v=1.7 s=0/1/0 size=33 n=4 p=- w=1.5"] = (26, "B6F0168813CA3FE4"),
        ["V v=1.7 s=0/1/0 size=33 n=4 p=- w=2"] = (26, "1D77D169BD0BBCA0"),
        ["V v=2 s=2/1/0 size=24 n=2 p=- w=1"] = (26, "B0853B35C807F140"),
        ["V v=2.4 s=-1/2/0 size=24 n=4 p=- w=1"] = (28, "3A96717B0B52A9D0"),
        ["V v=2.4 s=0/0/0 size=-24 n=4 p=- w=1"] = (19, "07DCCEB5ADB0681E"),
        ["V v=2.4 s=0/0/0 size=0 n=4 p=- w=1"] = (19, "25AFFD8CAAC0B94A"),
        ["V v=2.4 s=0/0/0 size=1 n=4 p=- w=1"] = (19, "DB9DB04625BBF810"),
        ["V v=2.4 s=0/0/0 size=101 n=4 p=- w=1"] = (19, "71BB9010AB3CA484"),
        ["V v=2.4 s=0/0/0 size=2 n=4 p=- w=1"] = (19, "62FCCC3FCACFF43B"),
        ["V v=2.4 s=0/0/0 size=24 n=4 p=- w=1"] = (19, "3E20976DB82281AC"),
        ["V v=2.4 s=0/0/0 size=25 n=4 p=- w=1"] = (19, "4684DD64FD268E9C"),
        ["V v=2.4 s=0/0/0 size=256 n=4 p=- w=1"] = (19, "F462F48350FB9FE4"),
        ["V v=2.4 s=0/0/0 size=257 n=4 p=- w=1"] = (19, "414FFE840546AFA6"),
        ["V v=2.4 s=0/0/0 size=3 n=4 p=- w=1"] = (19, "A642F7E0345F9D2E"),
        ["V v=2.4 s=0/0/0 size=5 n=4 p=- w=1"] = (19, "45A48F9837EEDD38"),
        ["V v=2.4 s=0/0/0 size=64 n=4 p=- w=1"] = (19, "3E8A9F4E1D2F172F"),
        ["V v=2.4 s=0/0/0 size=7 n=4 p=- w=1"] = (19, "493715ABD484E4CF"),
        ["V v=2.4 s=0/2/0 size=24 n=4 p=- w=1"] = (28, "3A96717B0B52A9D0"),
        ["V v=2.4 s=1/0/0 size=24 n=4 p=- w=1"] = (31, "4878728A3BD8AEC5"),
        ["V v=2.4 s=1/1/0 size=24 n=4 p=- w=1"] = (40, "A12042F756730F13"),
        ["V v=2.4 s=1/2/0 size=-24 n=4 p=- w=1"] = (40, "CF7721A8B0698F2F"),
        ["V v=2.4 s=1/2/0 size=0 n=4 p=- w=1"] = (40, "97E2618B092089BA"),
        ["V v=2.4 s=1/2/0 size=1 n=4 p=- w=1"] = (40, "469B1983799FF916"),
        ["V v=2.4 s=1/2/0 size=101 n=4 p=- w=1"] = (40, "5AD44B639F34BB84"),
        ["V v=2.4 s=1/2/0 size=2 n=4 p=- w=1"] = (40, "7BC42E6E5104803D"),
        ["V v=2.4 s=1/2/0 size=24 n=4 p=- w=0.5"] = (42, "513928EA8031A573"),
        ["V v=2.4 s=1/2/0 size=24 n=4 p=- w=1.5"] = (42, "8F9D82989C498112"),
        ["V v=2.4 s=1/2/0 size=24 n=4 p=- w=2"] = (42, "87CB483FA8B5F3F5"),
        ["V v=2.4 s=1/2/0 size=24 n=4 p=0 w=1"] = (40, "C3C64FB3178C280E"),
        ["V v=2.4 s=1/2/0 size=24 n=4 p=0.0004 w=1"] = (43, "0FD43F3F220B1371"),
        ["V v=2.4 s=1/2/0 size=24 n=4 p=1.5 w=1"] = (44, "F9FE923B8E3AE29F"),
        ["V v=2.4 s=1/2/0 size=24 n=4 p=3.25 w=1"] = (46, "3A6210EDB299637E"),
        ["V v=2.4 s=1/2/0 size=24 n=4 p=4 w=1"] = (44, "DB866F9EB5C5885C"),
        ["V v=2.4 s=1/2/0 size=25 n=4 p=- w=1"] = (40, "64078AD4F15A1FCE"),
        ["V v=2.4 s=1/2/0 size=256 n=4 p=- w=1"] = (40, "548550573E7F72E3"),
        ["V v=2.4 s=1/2/0 size=257 n=4 p=- w=1"] = (40, "46BA2C80B0640D5D"),
        ["V v=2.4 s=1/2/0 size=3 n=4 p=- w=1"] = (40, "F808B3D2BF873E70"),
        ["V v=2.4 s=1/2/0 size=5 n=4 p=- w=1"] = (40, "B506E954F24DE340"),
        ["V v=2.4 s=1/2/0 size=64 n=4 p=- w=1"] = (40, "D2F468590255A4FF"),
        ["V v=2.4 s=1/2/0 size=7 n=4 p=- w=1"] = (40, "29D74F66B56378BA"),
        ["V v=2.4 s=1/2/2 size=24 n=4 p=0 w=1"] = (40, "C3C64FB3178C280E"),
        ["V v=2.4 s=1/2/2 size=24 n=4 p=0.0004 w=1"] = (46, "188145EBBC448F42"),
        ["V v=2.4 s=1/2/2 size=24 n=4 p=1.5 w=1"] = (50, "CA8BF3FB842328EF"),
        ["V v=2.4 s=1/2/2 size=24 n=4 p=3.25 w=1"] = (58, "8E7A8D470F5582CD"),
        ["V v=2.4 s=1/2/2 size=24 n=4 p=4 w=1"] = (56, "42EBC5E6AA0B26FD"),
        ["V v=2.4 s=2/-3/0 size=24 n=4 p=- w=1"] = (31, "29968B80AEAFF0FD"),
        ["V v=2.4 s=2/2/0 size=24 n=4 p=- w=1"] = (40, "BD9B755509468FFC"),
        ["V v=2.4 s=3/1/0 size=24 n=4 p=- w=1"] = (40, "923264B3458EC00D"),
        ["V v=3.5 s=0/0/0 size=-24 n=4 p=- w=1"] = (20, "A93266B5D8D94A26"),
        ["V v=3.5 s=0/0/0 size=0 n=4 p=- w=1"] = (20, "11348F633873BF1A"),
        ["V v=3.5 s=0/0/0 size=1 n=4 p=- w=1"] = (20, "49D307630B496674"),
        ["V v=3.5 s=0/0/0 size=101 n=4 p=- w=1"] = (20, "17AE0F3919D87120"),
        ["V v=3.5 s=0/0/0 size=2 n=4 p=- w=1"] = (20, "642ACC0399E1D207"),
        ["V v=3.5 s=0/0/0 size=25 n=4 p=- w=1"] = (20, "2FD029E3BAF53376"),
        ["V v=3.5 s=0/0/0 size=256 n=4 p=- w=1"] = (20, "CC05C2FF8336D81F"),
        ["V v=3.5 s=0/0/0 size=257 n=4 p=- w=1"] = (20, "48412495D76BA900"),
        ["V v=3.5 s=0/0/0 size=3 n=4 p=- w=1"] = (20, "7C78E9BFAE526277"),
        ["V v=3.5 s=0/0/0 size=5 n=4 p=- w=1"] = (20, "FAE06CA523F69D70"),
        ["V v=3.5 s=0/0/0 size=64 n=4 p=- w=1"] = (20, "A3C39CDC3F66613B"),
        ["V v=3.5 s=0/0/0 size=7 n=4 p=- w=1"] = (20, "2A7D52863DBC180F"),
        ["V v=3.5 s=1/2/0 size=-24 n=4 p=- w=1"] = (44, "7F47464565A98445"),
        ["V v=3.5 s=1/2/0 size=0 n=4 p=- w=1"] = (44, "F17123C4394D61E2"),
        ["V v=3.5 s=1/2/0 size=1 n=4 p=- w=1"] = (44, "7B360DBAE6F82694"),
        ["V v=3.5 s=1/2/0 size=101 n=4 p=- w=1"] = (44, "F10B87AC5573E9B1"),
        ["V v=3.5 s=1/2/0 size=2 n=4 p=- w=1"] = (44, "4B385B68827E22D5"),
        ["V v=3.5 s=1/2/0 size=25 n=4 p=- w=1"] = (44, "AFBC747B5A77CB7D"),
        ["V v=3.5 s=1/2/0 size=256 n=4 p=- w=1"] = (44, "A452594A2E81FB0A"),
        ["V v=3.5 s=1/2/0 size=257 n=4 p=- w=1"] = (44, "FBAB87633F08E8FC"),
        ["V v=3.5 s=1/2/0 size=3 n=4 p=- w=1"] = (44, "7F6BB78E81E9BBAF"),
        ["V v=3.5 s=1/2/0 size=5 n=4 p=- w=1"] = (44, "00091A4BA16A39EA"),
        ["V v=3.5 s=1/2/0 size=64 n=4 p=- w=1"] = (44, "406727BDCB02166F"),
        ["V v=3.5 s=1/2/0 size=7 n=4 p=- w=1"] = (44, "033458DF46DB9124"),
        ["V v=4 s=-1/2/0 size=24 n=4 p=- w=1"] = (30, "8EB1F88FE625DB02"),
        ["V v=4 s=0/0/0 size=24 n=4 p=- w=1"] = (18, "3F418391A6D0624F"),
        ["V v=4 s=0/2/0 size=24 n=4 p=- w=1"] = (30, "8EB1F88FE625DB02"),
        ["V v=4 s=1/0/0 size=24 n=4 p=- w=1"] = (30, "E7B436159BE655F5"),
        ["V v=4 s=1/1/0 size=24 n=4 p=- w=1"] = (42, "4D1D140EE3CD4E0E"),
        ["V v=4 s=2/-3/0 size=24 n=4 p=- w=1"] = (30, "CD551D67745DED4F"),
        ["V v=4 s=2/2/0 size=24 n=4 p=- w=1"] = (42, "9CF9588670232EE7"),
        ["V v=4 s=3/1/0 size=24 n=4 p=- w=1"] = (42, "760146F80EEF6E58"),
        ["V v=6.4 s=1/2/0 size=24 n=7 p=- w=1"] = (68, "418F982C309C9F0E"),
        ["V v=7 s=2/1/0 size=24 n=7 p=- w=1"] = (66, "F12D815ED5A6FDF8"),
    };
}
