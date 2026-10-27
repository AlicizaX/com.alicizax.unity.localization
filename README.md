# AlicizaX Localization

本包提供运行时 `LocalizationKit`、TMP 的 `LocalizedText` 绑定和编辑器预览。TMP 是可选依赖；项目提供 TextMeshPro 或 UGUI 2.0+ 后才会编译 `LocalizedText` 和对应的编辑器预览。安装包后即可编译；需要文案时，再使用项目已有的外部 `Config` 目录。多语言规则写在 `Config/config.ini`，与普通 Luban 表一起生成。

## 1. 准备表格

在 `Config/config.ini` 的 `[localization]` 中填写 `languages`、`default_locale` 和 `runtime_options_out`。路径相对于 `Config` 目录。`runtime_options_out` 指向 Unity 项目 `Assets/Resources` 下的 `RuntimeOptions.json`，本项目使用 `../Client/Assets/Resources/Localization/RuntimeOptions.json`；编辑器预览也读取这份文件。

文案工作簿使用 `Texts` 工作表，前两行分别以 `##var`、`##type` 开头，列名包含 `key` 和配置中的各语言。表格格式可参考 AlicizaXTemplate 项目的 `Config/Excels/Localization`，其他项目按此格式新建。不要添加 `layer` 列。翻译单元格留空时显示 Key；填入 `""` 时显示空字符串。

本项目的 `[constant.dir.main]` 会扫描 `Config/Excels/Localization` 当前层、文件名以 `LocalizationConst` 开头的 `.xlsx`。例如新增 `LocalizationConst_Shop.xlsx` 会自动纳入生成。需要为某张表单独指定输出时，添加 `[constant.file.shop]` 并填写该表的 `path`、`data_out` 和 `code_out`；`file` 规则优先于目录规则。`ordinary.dir` 适合只导出词库、不生成常量访问器的普通文案表。`data_out` 必须包含 `{locale}` 并以 `.bytes` 结尾；`code_out` 是 `.cs` 文件。其他规则见 `Config/config.ini` 的注释。

生成时会检查跨表重复 Key、翻译格式参数，以及普通 Luban 表中 `text` 字段引用的 Key。

## 2. 生成和检查

首次构建或更新 Luban 工具时，在 `Config` 目录运行 `.\build.bat`（Windows）或 `bash build.sh`（macOS）。需要 .NET 8 SDK、.NET 8 Runtime 和 Git；macOS 还需要 curl。成功后编译产物位于 `Config/Tools`。

在同一目录生成普通表和多语言词库：

```text
Windows: .\gen-all.bat
macOS:   bash gen-all.sh
```

提交或构建前检查生成文件是否与表格一致：

```text
Windows: .\gen-all.bat --check
macOS:   bash gen-all.sh --check
```

本项目可通过 Unity 菜单 **AlicizaX / Config / Generate Config** 运行 `gen-all`，通过 **Check Generated Files** 运行 `--check`。构建前的检查也由项目的 BuildCLI 执行。`gen-all` 将词库相对路径写入 `RuntimeOptions.json`；包内编辑器工具读取它预览文案，无需解析 `config.ini`。`--check` 只比较结果，不修改生成文件。

编辑器按文件名查找 `RuntimeOptions.json`，其中的词库路径相对于该文件所在目录；同一项目只保留一份。升级旧项目时删除原来的 `AlicizaXLocalizationPreview.json`。调整输出位置时，也要更新项目启动代码的 `Resources.Load` 路径和 BuildCLI 的构建语言校验路径。

## 3. 默认语言与运行时加载

生成的 `RuntimeOptions.json` 保存支持的语言、`default_locale` 指定的项目默认语言和编辑器预览用的词库路径；运行时不根据这些路径加载词库。BuildCLI 可通过 `UCL_PARAM_LANGUAGE` 指定本次构建的首启语言；它必须在支持列表中。构建时会把该语言作为一个小文件放进 Player，不会改写 `RuntimeOptions.json`。启动时优先使用玩家已保存且仍受支持的语言，其次使用本次构建语言，最后使用项目默认语言。未指定构建语言时，首次启动使用项目默认语言；Unity 编辑器继续使用编辑器的语言选择。

在启动流程的异步方法中，先加载更新界面需要的词库。下面的地址对应本项目的 `Config/config.ini`；Resources 地址相对于 `Resources` 目录，省略 `.bytes`：

```csharp
using AlicizaX.Localization.Runtime;
using UnityEngine;

TextAsset options = Resources.Load<TextAsset>("Localization/RuntimeOptions");
string json = options.text;
Resources.UnloadAsset(options);
await LocalizationKit.InitializeAsync(
    json, new ResourcesCatalogSource("Localization/Update_{locale}"));
```

热更资源可用后，再追加其余词库：

```csharp
await LocalizationKit.AddCatalogAsync(new ResourceCatalogSource("Game_{locale}"));
```

`ResourceCatalogSource` 使用框架的 `GameApp.Resource`。使用其他资源系统时，实现 `ILocalizationCatalogSource.LoadAsync(locale)`。切换语言调用 `await LocalizationKit.SetLanguageAsync("English")`，取文案调用 `LocalizationKit.Get(key)`。普通配置服务初始化后，调用 `configService.SetLocalizationChange(LocalizationKit.Get, LocalizationKit.SubscribeChanged)`，让配置中的文案随语言刷新。

## 4. 绑定 TMP

给带 `TMP_Text` 的对象添加 `LocalizedText`，在 Inspector 的 **Language Key** 中搜索并选择 Key；文案有 `{0}` 等占位符时填写 Arguments。运行 `gen-all` 后，编辑器从 `RuntimeOptions.json` 读取语言和词库位置。编辑器顶部的语言菜单可切换 Scene 和 Prefab 预览，预览不会写入 TMP 的持久化文本。只安装包、尚未生成 `RuntimeOptions.json` 时，包仍可编译；已有 Key 显示 Key 本身。运行时可通过 `SetKey`、`SetArguments` 或 `SetContent` 修改绑定。
