using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;

namespace HeatonCA.Tests
{
    /// <summary>
    /// The app's internal API contract, pinned by reflection. Every screen in
    /// Phase 3 is written by a different agent against
    /// <see cref="INavigator"/>, <see cref="IAppView"/>, and
    /// <see cref="AppServices"/>, so a quiet rename or an extra parameter would
    /// surface as a merge conflict rather than a compile error in only one
    /// worktree. These tests spell the contract out member by member: adding,
    /// removing, or reshaping anything on those three types fails here first,
    /// which makes the change a deliberate edit to this file instead of drift.
    ///
    /// A failure is not a bug in the test — it is the reminder that the
    /// contract in the plan and the code have parted company. Fix the code, or
    /// change both together.
    /// </summary>
    public class AppContractsTests
    {
        [Test]
        public void NavigatorExposesExactlyTheDocumentedCalls()
        {
            string[] expected =
            {
                "Void ShowHome()",
                "Void ShowGallery()",
                "Void ShowSimulator(String, Boolean)",
                "Void ShowRuleDecoder(String)",
                "Void ShowEvolve()",
                "Void ShowSettings()",
                "Void ShowAbout()",
                "Void Back()",
                "Task ShowAlertAsync(String, String)",
                "Task<Boolean> ShowConfirmAsync(String, String, String, String)",
                "Void Status(String)",
                "Void OpenUrl(String)",
            };
            CollectionAssert.AreEquivalent(expected, MethodSignatures(typeof(INavigator)));
            Assert.AreEqual(
                0, typeof(INavigator).GetProperties(Declared).Length,
                "INavigator is a command surface: no properties.");
            Assert.AreEqual(
                0, typeof(INavigator).GetEvents(Declared).Length,
                "INavigator is a command surface: no events.");
        }

        [Test]
        public void NavigatorOptionalArgumentsKeepTheirDefaults()
        {
            // The defaults are load-bearing: ShowSimulator() from the Home
            // button must leave the lattice paused on the current rule, while
            // the gallery passes (rule, true) to auto-start it (PyQt
            // display_rule), and a confirm dialog labels its buttons OK/Cancel
            // unless the caller says otherwise.
            ParameterInfo[] simulator = Method(typeof(INavigator), "ShowSimulator").GetParameters();
            Assert.AreEqual("rule", simulator[0].Name);
            Assert.IsTrue(simulator[0].HasDefaultValue, "rule is optional");
            Assert.IsNull(simulator[0].DefaultValue, "rule defaults to null (keep the current rule)");
            Assert.AreEqual("autoStart", simulator[1].Name);
            Assert.IsTrue(simulator[1].HasDefaultValue, "autoStart is optional");
            Assert.AreEqual(false, simulator[1].DefaultValue, "autoStart defaults to false");

            ParameterInfo[] confirm = Method(typeof(INavigator), "ShowConfirmAsync").GetParameters();
            Assert.AreEqual("title", confirm[0].Name);
            Assert.AreEqual("message", confirm[1].Name);
            Assert.AreEqual("ok", confirm[2].Name);
            Assert.AreEqual("OK", confirm[2].DefaultValue);
            Assert.AreEqual("cancel", confirm[3].Name);
            Assert.AreEqual("Cancel", confirm[3].DefaultValue);
            // The defaults are spelled as AppStrings constants in the source, so
            // the two button labels stay auditable in the one strings file. The
            // compiler bakes their values into this metadata, which is why the
            // literals above are the real pin and this is the coupling check.
            Assert.AreEqual(AppStrings.Ok, confirm[2].DefaultValue);
            Assert.AreEqual(AppStrings.Cancel, confirm[3].DefaultValue);

            Assert.AreEqual("rule", Method(typeof(INavigator), "ShowRuleDecoder").GetParameters()[0].Name);
            Assert.IsFalse(
                Method(typeof(INavigator), "ShowRuleDecoder").GetParameters()[0].HasDefaultValue,
                "the decoder always decodes a named rule");
        }

        [Test]
        public void NavigatorDialogsAreAwaitable()
        {
            // TaskCompletionSource overlays, not blocking dialogs: awaiting one
            // must never need a worker thread, because WebGL has none.
            Assert.AreEqual(
                typeof(Task), Method(typeof(INavigator), "ShowAlertAsync").ReturnType);
            Assert.AreEqual(
                typeof(Task<bool>), Method(typeof(INavigator), "ShowConfirmAsync").ReturnType);
        }

        [Test]
        public void AppViewExposesExactlyTheDocumentedMembers()
        {
            string[] expectedMethods =
            {
                "Void Show()",
                "Void Hide()",
                "Void Tick(Single)",
                "Void ApplyLayout(Boolean, Boolean)",
            };
            CollectionAssert.AreEquivalent(expectedMethods, MethodSignatures(typeof(IAppView)));

            PropertyInfo[] properties = typeof(IAppView).GetProperties(Declared);
            Assert.AreEqual(2, properties.Length, "IAppView has exactly Root and Visible.");
            PropertyInfo root = Property(typeof(IAppView), "Root");
            Assert.AreEqual(typeof(GameObject), root.PropertyType);
            Assert.IsTrue(root.CanRead);
            Assert.IsFalse(root.CanWrite, "the view owns its root; the controller only reads it");
            PropertyInfo visible = Property(typeof(IAppView), "Visible");
            Assert.AreEqual(typeof(bool), visible.PropertyType);
            Assert.IsTrue(visible.CanRead);
            Assert.IsFalse(visible.CanWrite, "visibility changes through Show/Hide, not a setter");

            ParameterInfo[] layout = Method(typeof(IAppView), "ApplyLayout").GetParameters();
            Assert.AreEqual("portrait", layout[0].Name);
            Assert.AreEqual("phone", layout[1].Name);
            Assert.AreEqual("dt", Method(typeof(IAppView), "Tick").GetParameters()[0].Name);
        }

        [Test]
        public void AppServicesExposesExactlyTheDocumentedSlots()
        {
            var expected = new Dictionary<string, string>
            {
                { "Host", "SimulationHost" },
                { "Blitter", "FrameBlitter" },
                { "Evolve", "EvolveHost" },
                { "Store", "IAppStore" },
                { "Thumbnails", "FindsThumbnailer" },
                { "SelfCheckReport", "Func<String>" },
                { "SelfCheckPassed", "Func<Boolean>" },
            };
            PropertyInfo[] properties = typeof(AppServices).GetProperties(Declared);
            Assert.AreEqual(expected.Count, properties.Length, "AppServices carries exactly seven slots.");
            foreach (PropertyInfo property in properties)
            {
                Assert.IsTrue(
                    expected.ContainsKey(property.Name),
                    "undocumented AppServices slot: " + property.Name);
                Assert.AreEqual(
                    expected[property.Name], TypeName(property.PropertyType),
                    "wrong type on AppServices." + property.Name);
                Assert.IsTrue(property.CanRead, property.Name + " must be readable");
                Assert.IsTrue(
                    property.CanWrite,
                    property.Name + " must be settable: the controller fills the bag with an initializer");
            }

            Assert.AreEqual(
                0, typeof(AppServices).GetFields(Declared).Length,
                "AppServices exposes properties, not fields.");
            Assert.AreEqual(
                0, MethodSignatures(typeof(AppServices)).Count,
                "AppServices is a plain bag: no behavior of its own.");
            Assert.IsTrue(typeof(AppServices).IsSealed, "AppServices is not a base class.");
            Assert.IsNotNull(
                typeof(AppServices).GetConstructor(Type.EmptyTypes),
                "AppServices is built with an object initializer.");
        }

        [Test]
        public void ScreenIdCoversEveryScreenWithHomeAsTheRoot()
        {
            string[] expected =
            {
                "Home", "Gallery", "Simulator", "RuleDecoder", "Evolve", "Settings", "About",
            };
            CollectionAssert.AreEqual(expected, Enum.GetNames(typeof(ScreenId)));
            // Home is the back-stack root, and the default(ScreenId) an
            // uninitialized field carries must be that root, never a real screen.
            Assert.AreEqual(0, (int)ScreenId.Home);
            Assert.AreEqual(ScreenId.Home, default(ScreenId));
        }

        [Test]
        public void AppViewBaseImplementsTheViewContractForEveryScreen()
        {
            Assert.IsTrue(typeof(AppViewBase).IsAbstract, "AppViewBase is plumbing, not a screen.");
            Assert.IsTrue(typeof(IAppView).IsAssignableFrom(typeof(AppViewBase)));

            ConstructorInfo[] constructors = typeof(AppViewBase).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.AreEqual(1, constructors.Length, "one constructor, so every view takes the same one.");
            Assert.IsTrue(constructors[0].IsFamily, "the base constructor is protected.");
            CollectionAssert.AreEqual(FixedViewConstructor, ParameterTypes(constructors[0]));

            // Every default is overridable: a screen customizes what it needs
            // and inherits the rest.
            foreach (string name in new[] { "Show", "Hide", "Tick", "ApplyLayout" })
            {
                Assert.IsTrue(
                    Method(typeof(AppViewBase), name).IsVirtual,
                    name + " must be virtual so a screen can extend it");
            }
            PropertyInfo root = Property(typeof(AppViewBase), "Root");
            Assert.IsTrue(root.CanWrite, "a derived view assigns Root once it has built its panel");
            Assert.IsTrue(
                root.GetSetMethod(nonPublic: true).IsFamily,
                "Root's setter is protected: nothing outside the view reparents a screen");
        }

        [Test]
        public void EveryViewDeclaresTheFixedConstructor()
        {
            // The controller creates screens uniformly, so a view may not invent
            // its own constructor shape: state it needs comes through
            // AppServices, and callbacks through INavigator. Vacuous until the
            // Phase 3 screens land in this assembly, which is the point.
            int checkedViews = 0;
            foreach (Type type in AppAssemblyTypes())
            {
                if (!type.IsClass || type.IsAbstract || !typeof(IAppView).IsAssignableFrom(type))
                {
                    continue;
                }
                checkedViews++;
                ConstructorInfo fixedCtor = type.GetConstructor(FixedViewConstructor);
                Assert.IsNotNull(
                    fixedCtor,
                    type.Name + " must declare the fixed view constructor "
                    + "(RectTransform parent, INavigator nav, AppServices services).");
                Assert.AreEqual(
                    1, type.GetConstructors().Length,
                    type.Name + " must expose only the fixed view constructor.");
            }
            Assert.GreaterOrEqual(checkedViews, 0);
        }

        [Test]
        public void NativeMenusExposesTheThreeSeamEntryPoints()
        {
            // The desktop menu seam: platform bodies arrive as partial methods
            // in the P2 files, so this public surface must stay exactly three
            // calls and must not grow a platform query the app could branch on.
            string[] expected =
            {
                "Void Install(INavigator)",
                "Void Poll()",
                "Void Uninstall()",
            };
            List<string> actual = MethodSignatures(
                typeof(NativeMenus), BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            CollectionAssert.AreEquivalent(expected, actual);
            Assert.IsTrue(
                typeof(NativeMenus).IsAbstract && typeof(NativeMenus).IsSealed,
                "NativeMenus is a static class.");
        }

        // ---- reflection helpers ------------------------------------------------------

        private const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        /// <summary>The one constructor shape every screen takes.</summary>
        private static readonly Type[] FixedViewConstructor =
        {
            typeof(RectTransform), typeof(INavigator), typeof(AppServices),
        };

        private static List<string> MethodSignatures(Type type)
        {
            return MethodSignatures(type, Declared);
        }

        /// <summary>
        /// "ReturnType Name(Param, Param)" for every declared method that is not
        /// a property or event accessor.
        /// </summary>
        private static List<string> MethodSignatures(Type type, BindingFlags flags)
        {
            var signatures = new List<string>();
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }
                var text = new StringBuilder();
                text.Append(TypeName(method.ReturnType)).Append(' ').Append(method.Name).Append('(');
                ParameterInfo[] parameters = method.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        text.Append(", ");
                    }
                    text.Append(TypeName(parameters[i].ParameterType));
                }
                signatures.Add(text.Append(')').ToString());
            }
            return signatures;
        }

        /// <summary>Readable type name: "String", "Task&lt;Boolean&gt;", "Func&lt;String&gt;".</summary>
        private static string TypeName(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }
            string bare = type.Name.Substring(0, type.Name.IndexOf('`'));
            var text = new StringBuilder(bare).Append('<');
            Type[] arguments = type.GetGenericArguments();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }
                text.Append(TypeName(arguments[i]));
            }
            return text.Append('>').ToString();
        }

        private static MethodInfo Method(Type type, string name)
        {
            MethodInfo method = type.GetMethod(
                name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.IsNotNull(method, type.Name + " must declare " + name);
            return method;
        }

        private static PropertyInfo Property(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                | BindingFlags.DeclaredOnly);
            Assert.IsNotNull(property, type.Name + " must declare " + name);
            return property;
        }

        private static Type[] ParameterTypes(MethodBase method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            var types = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                types[i] = parameters[i].ParameterType;
            }
            return types;
        }

        /// <summary>
        /// Every type in the app assembly. A half-loaded assembly (a stale
        /// player DLL in the Library) reports what it could load instead of
        /// throwing, so this test never masks a real contract break with a
        /// loader error.
        /// </summary>
        private static IEnumerable<Type> AppAssemblyTypes()
        {
            try
            {
                return typeof(IAppView).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                var loaded = new List<Type>();
                foreach (Type type in e.Types)
                {
                    if (type != null)
                    {
                        loaded.Add(type);
                    }
                }
                return loaded;
            }
        }
    }
}
