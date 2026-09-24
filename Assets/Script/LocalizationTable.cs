using System.Collections.Generic;

/// <summary>
/// v1 常驻 UI 文案的翻译表——纯 C# 静态字典，不做成 ScriptableObject 资产:翻译内容不需要
/// 在 Inspector 里手调，代码里直接维护比让用户去 Editor 建一份资产省一轮来回，跟
/// `GoalSettings.BuildDefaultGoals()` 那种"代码里给默认值"是同一个取舍，只是这里没有
/// 资产覆盖这一层，直接就是最终数据源。
///
/// 每个 key 对应一个长度 7 的数组，下标顺序跟 `Locale` 枚举顺序一致(English/
/// SimplifiedChinese/TraditionalChinese/Japanese/German/French/Spanish)——新增语言只能
/// 往数组末尾加，不能中间插(见 `Locale` 枚举注释)。带 `{0}` 占位符的字符串在使用处用
/// `string.Format` 或插值填入实际数值，翻译时占位符本身不用动。
/// </summary>
public static class LocalizationTable
{
    static readonly Dictionary<string, string[]> Entries = new Dictionary<string, string[]>
    {
        // Tab 栏四个标题
        ["tab.goals"] = new[] { "Goals", "目标", "目標", "目標", "Ziele", "Objectifs", "Objetivos" },
        ["tab.settings"] = new[] { "Settings", "设置", "設定", "設定", "Einstellungen", "Paramètres", "Ajustes" },
        ["tab.language"] = new[] { "Language", "语言", "語言", "言語", "Sprache", "Langue", "Idioma" },
        ["tab.stats"] = new[] { "Stats", "统计", "統計", "統計", "Statistik", "Statistiques", "Estadísticas" },

        // Menu / 暂停面板按钮
        ["button.menu"] = new[] { "Menu", "菜单", "選單", "メニュー", "Menü", "Menu", "Menú" },
        ["button.back"] = new[] { "Back", "返回", "返回", "戻る", "Zurück", "Retour", "Atrás" },
        ["button.resume"] = new[] { "Resume", "继续", "繼續", "再開", "Fortsetzen", "Reprendre", "Reanudar" },
        ["button.restart"] = new[] { "Restart", "重新开始", "重新開始", "リスタート", "Neustart", "Recommencer", "Reiniciar" },
        ["button.home"] = new[] { "Home", "主页", "主頁", "ホーム", "Start", "Accueil", "Inicio" },

        // Settings 面板
        ["settings.sounds"] = new[] { "Sounds", "音效", "音效", "効果音", "Klänge", "Sons", "Sonidos" },
        ["settings.music"] = new[] { "Music", "音乐", "音樂", "音楽", "Musik", "Musique", "Música" },
        ["settings.boostButton"] = new[] { "Boost Button", "加速按钮", "加速按鈕", "ブーストボタン", "Boost-Taste", "Bouton Turbo", "Botón de Turbo" },
        ["settings.left"] = new[] { "Left", "左", "左", "左", "Links", "Gauche", "Izquierda" },
        ["settings.right"] = new[] { "Right", "右", "右", "右", "Rechts", "Droite", "Derecha" },

        // Run Summary 结算面板
        ["runsummary.title"] = new[] { "Run Complete", "本局结束", "本局結束", "ラン終了", "Lauf beendet", "Course terminée", "Carrera completa" },
        ["runsummary.distance"] = new[] { "Distance Travelled ({0}m)", "行驶距离 ({0}m)", "行駛距離 ({0}m)", "走行距離 ({0}m)", "Zurückgelegte Strecke ({0} m)", "Distance parcourue ({0} m)", "Distancia recorrida ({0} m)" },
        ["runsummary.gears"] = new[] { "Gears Collected ({0})", "收集齿轮 ({0})", "收集齒輪 ({0})", "獲得ギア数 ({0})", "Gesammelte Zahnräder ({0})", "Engrenages collectés ({0})", "Engranajes recolectados ({0})" },
        ["runsummary.nodes"] = new[] { "Nodes Passed ({0})", "经过站点 ({0})", "經過站點 ({0})", "通過ノード数 ({0})", "Passierte Stationen ({0})", "Stations franchies ({0})", "Estaciones superadas ({0})" },
        ["runsummary.maxhp"] = new[] { "Max HP ({0})", "最大生命值 ({0})", "最大生命值 ({0})", "最大HP ({0})", "Max. HP ({0})", "PV Max ({0})", "PV Máx. ({0})" },
        ["runsummary.landingQuality"] = new[] { "Landing Quality", "落地质量", "落地品質", "着地クオリティ", "Landequalität", "Qualité d'atterrissage", "Calidad de aterrizaje" },
        ["runsummary.trickScore"] = new[] { "Trick Score", "特技得分", "特技得分", "トリックスコア", "Trickpunkte", "Score de figures", "Puntos de trucos" },
        ["runsummary.trickScoreBest"] = new[] { "Trick Score - best: {0}", "特技得分 - 最佳: {0}", "特技得分 - 最佳: {0}", "トリックスコア - 最高: {0}", "Trickpunkte - Beste: {0}", "Score de figures - meilleur : {0}", "Puntos de trucos - mejor: {0}" },
        ["runsummary.nearMiss"] = new[] { "Near Miss", "贴身险", "驚險擦身", "ニアミス", "Beinahe-Unfall", "Évitement de justesse", "Casi choque" },
        ["runsummary.newDistanceRecord"] = new[] { "New Distance Record", "新的最远距离纪录", "新的最遠距離紀錄", "新記録：最長距離", "Neuer Distanzrekord", "Nouveau record de distance", "Nuevo récord de distancia" },
        ["runsummary.total"] = new[] { "Total", "总计", "總計", "合計", "Gesamt", "Total", "Total" },
        ["runsummary.newHighScore"] = new[] { "New High Score", "新纪录！", "新紀錄！", "新記録！", "Neuer Highscore", "Nouveau meilleur score", "Nueva puntuación máxima" },
        ["runsummary.playAgain"] = new[] { "Play Again", "再来一局", "再來一局", "もう一度プレイ", "Erneut spielen", "Rejouer", "Jugar de nuevo" },

        // Goals（Tab + 结算前的 Recap 面板共用）
        ["goals.level"] = new[] { "Level {0}", "第 {0} 关", "第 {0} 關", "レベル {0}", "Level {0}", "Niveau {0}", "Nivel {0}" },
        ["goalsrecap.title"] = new[] { "GOALS", "目标", "目標", "目標", "ZIELE", "OBJECTIFS", "OBJETIVOS" },
        ["goalsrecap.next"] = new[] { "Next", "下一步", "下一步", "次へ", "Weiter", "Suivant", "Siguiente" },

        // Stats 面板行标签
        ["stats.bestDistance"] = new[] { "Best Distance", "最远距离", "最遠距離", "最長距離", "Beste Distanz", "Meilleure distance", "Mejor distancia" },
        ["stats.bestScore"] = new[] { "Best Score", "最高分", "最高分", "最高スコア", "Highscore", "Meilleur score", "Mejor puntuación" },
        ["stats.bestTrickScore"] = new[] { "Best Trick Score", "最佳特技得分", "最佳特技得分", "最高トリックスコア", "Beste Trickpunkte", "Meilleur score de figures", "Mejores puntos de trucos" },
        ["stats.totalDistance"] = new[] { "Total Distance", "累计里程", "累計里程", "総走行距離", "Gesamtdistanz", "Distance totale", "Distancia total" },
        ["stats.totalRuns"] = new[] { "Total Runs", "总局数", "總局數", "総プレイ回数", "Gesamtläufe", "Courses totales", "Carreras totales" },
        ["stats.tricksPerformed"] = new[] { "Tricks Performed", "完成特技次数", "完成特技次數", "トリック実行回数", "Ausgeführte Tricks", "Figures réalisées", "Trucos realizados" },
        ["stats.perfectLandings"] = new[] { "Perfect Landings", "完美落地次数", "完美落地次數", "パーフェクト着地数", "Perfekte Landungen", "Atterrissages parfaits", "Aterrizajes perfectos" },
        ["stats.goodLandings"] = new[] { "Good Landings", "良好落地次数", "良好落地次數", "グッド着地数", "Gute Landungen", "Bons atterrissages", "Aterrizajes buenos" },
        ["stats.notBadLandings"] = new[] { "Not Bad Landings", "及格落地次数", "及格落地次數", "ノットバッド着地数", "Okay-Landungen", "Atterrissages corrects", "Aterrizajes aceptables" },
        ["stats.nearMisses"] = new[] { "Near Misses", "贴身险次数", "驚險擦身次數", "ニアミス回数", "Beinahe-Unfälle", "Évitements de justesse", "Casi choques" },
        ["stats.nodesReached"] = new[] { "Nodes Reached", "经过站点数", "經過站點數", "到達ノード数", "Erreichte Stationen", "Stations atteintes", "Estaciones alcanzadas" },
        ["stats.gearsCollected"] = new[] { "Gears Collected", "收集齿轮数", "收集齒輪數", "獲得ギア数", "Gesammelte Zahnräder", "Engrenages collectés", "Engranajes recolectados" },
    };

    public static string Lookup(string key, Locale locale)
    {
        if (!Entries.TryGetValue(key, out string[] translations)) return key;

        int index = (int)locale;
        return index >= 0 && index < translations.Length ? translations[index] : translations[0];
    }
}
