using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bard;
using Characters;
using Combat.Characters;
using Status;
using UI;
using UniRx;
using UnityEngine;

public enum CombatPhase {
    SelectMelodies,
    RhythmGame,
    ExecMelodies,
    TurnFight
}

public class CombatManager : MonoBehaviour {
    public BardControl bard; //TODO init bard in GameManager
    public List<List<CharacterControl>> teams = new List<List<CharacterControl>>(2);
    public GameObject playerTeamGo, enemyTeamGo;
    public RectTransform playerTeamUiContainer;
    public RectTransform enemyTeamUiContainer;
    public ReactiveProperty<CombatPhase> combatPhase = new ReactiveProperty<CombatPhase>(CombatPhase.SelectMelodies);
    
    private List<CharacterBase> clientsMenu;
    private List<CharacterBase> currentMonsters;

    [Header("Combat Testing Only")]
    [SerializeField] public bool ignoreGameManager = false;
    [SerializeField] private CharacterBase[] forcedClients = new CharacterBase[3];
    [SerializeField] private CharacterBase[] forcedMonsters = new CharacterBase[3];
    
    public async Task ExecTurn() {
        combatPhase.Value = CombatPhase.RhythmGame;
        await bard.StartRhythmGame();
        await Utils.AwaitObservable(Observable.Timer(TimeSpan.FromSeconds(1600f / 200f))); // wait actual rhythm end
        combatPhase.Value = CombatPhase.ExecMelodies;
        await bard.ExecMelodies();
        bard.Reset();
        combatPhase.Value = CombatPhase.TurnFight;
        await ResolveTurnFight();
        combatPhase.Value = CombatPhase.SelectMelodies;
    }

    private async Task ResolveTurnFight() {
        //sort characters
        List<CharacterControl> characters = new List<List<CharacterControl>>(4) {
            [0] = teams
                .SelectMany(t =>
                    t.Where(c => c.HasEffect(EffectType.Rushing)))
                .ToList(),
            [1] = teams
                .SelectMany(t =>
                    t.Where(c => c.influenced))
                .ToList(),
            [2] = teams
                .SelectMany(t =>
                    t.Where(c =>
                        !c.HasEffect(EffectType.Rushing) && !c.influenced && !c.HasEffect(EffectType.Lagging)))
                .ToList(),
            [3] = teams
                .SelectMany(t =>
                    t.Where(c => c.HasEffect(EffectType.Lagging)))
                .ToList()
        }.SelectMany(t => t).ToList();
        
        // Apply status effects to all characters
        foreach (var character in characters) {
            var l = character.transform.Find("HighLight").gameObject;
            l.SetActive(true);

            await character.EffectsTurnStart();

            l.SetActive(false);
        }

        // Execute all character skills
        foreach (var character in characters) {
            var l = character.transform.Find("HighLight").gameObject;
            l.SetActive(true);

            await character.ExecTurn();

            l.SetActive(false);
        }
    }

    public void Remove(CharacterControl characterControl) {
        int charTeam = (int) characterControl.team;
        if(charTeam == 0)
            GameManager.quest.FightMax[characterControl.clientNumber] = GameManager.CurrentFight + 1; //decreasing the number of total fight

        Destroy(characterControl.gameObject);
        teams[charTeam].Remove(characterControl);

        if (teams[charTeam].Count == 0) {
            GetComponent<GameManager>().ChangeCombat();
            if (!GameManager.QuestState) {
                GetComponent<GameManager>().ChangeScene(2);
            } else {
                int gold = GetComponent<GameManager>().CalculateGold();
                GetComponent<GameManager>().ChangeScene(0);
            }
        }
    }

    #region Unity

    public static CombatManager Instance { get; private set; }

    public void Awake() {
        Instance = this;
    }

    private void Start() {

        clientsMenu = new List<CharacterBase>();
        currentMonsters = new List<CharacterBase>();
        if (ignoreGameManager) {
            //init characters based on editor (without gameManager)
            foreach (var client in  forcedClients)
                clientsMenu.Add(client);
        
            foreach (var monster in forcedMonsters)
                currentMonsters.Add(monster);
        } else {
            //init characters based on gameManager (loaded from the Expedition)
            foreach (var client in  GameManager.clients)
                clientsMenu.Add(client);
        
            foreach (var monster in GameManager.quest.fightManager.fights[GameManager.CurrentFight].monsters)
                currentMonsters.Add(monster);
        }

        teams = new List<List<CharacterControl>> {new List<CharacterControl>(), new List<CharacterControl>()};

        int i = 0;
        foreach (Transform t in playerTeamGo.transform)
        {
            SetupCharacterControl(t, clientsMenu, i, Team.PlayerTeam);
            ++i;
        }

        i = 0;
        foreach (Transform t in enemyTeamGo.transform) {
            SetupCharacterControl(t, currentMonsters, i, Team.EmemyTeam);
            ++i;
        }
    }

    private async void SetupCharacterControl(Transform characterTransform, IReadOnlyList<CharacterBase> team, int i, Team charTeam) {
        string charPrefabName = charTeam == Team.PlayerTeam ? "PlayerTeamCharacterPrefab" : "EnemyCharacterPrefab";
        string charPrefabUiName = charTeam == Team.PlayerTeam ? "PlayerTeamCharacterUI" : "EnemyCharacterUI";

        // instantiate the character prefab
        var characterGameObject = Instantiate(await Utils.LoadResource<GameObject>(charPrefabName), characterTransform);

        // Load the character 3d model
        var model = Instantiate(await Utils.LoadResource<GameObject>(team[i].Character.modelName), characterGameObject.transform);
        model.AddComponent<Animator>().runtimeAnimatorController = await Utils.LoadResource<RuntimeAnimatorController>("Animations/Character");

        // Init the character control
        CharacterControl character = characterGameObject.GetComponent<CharacterControl>();
        character.Init(team[i].Character, team[i].SkillWheel);
        character.team = charTeam;
        teams[(int) charTeam].Add(character);

        // instantiate the UI on the canvas
        var charUi = characterGameObject.transform.Find(charPrefabUiName);
        charUi.SetParent(charTeam == Team.PlayerTeam ? playerTeamUiContainer : enemyTeamUiContainer);
        charUi.localScale = Vector3.one;
        charUi.localEulerAngles = Vector3.zero;
        characterGameObject.GetComponent<CharacterVfx>().Init(character);
        SlotUi slotUi = charUi.GetComponentInChildren<SlotUi>();
        slotUi.Init(character);
    }

    #endregion
}
