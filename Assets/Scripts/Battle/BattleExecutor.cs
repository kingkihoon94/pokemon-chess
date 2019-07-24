﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

using Unity.Jobs;
public class BattleExecutor : MonoBehaviour
{
    public bool isInBattle = false;

    private Pokemon[,] pokemonsInBattle;
    private ChessBoard chessBoard;
    private float moveTimer;
    public float moveTime = 1f;
    private Trainer challenger;

    private Dictionary<Pokemon, Vector2Int> liveOwnerPokemons;
    private Dictionary<Pokemon, Vector2Int> liveChallengerPokemons;

    private BattleCallbackHandler callbackHandler;
    private IEnumerator battleCoroutine;

    private Trainer winner;

    private BattleManager battleManager;
    
    private enum MoveDirection
    {
        Up, Down, Left, Right, None
    }

    private Dictionary<Pokemon, MoveDirection> pokemonPreviousMove;
    public PokemonUIManager PokemonUIManager;

    void Awake()
    {
        battleManager = FindObjectOfType<BattleManager>();
        callbackHandler = GetComponent<BattleCallbackHandler>();
        chessBoard = GetComponent<ChessBoard>();
    }
    public void ReadyBattle(Trainer challenger)
    {
        isInBattle = true;

        pokemonsInBattle = new Pokemon[8, 8];

        liveOwnerPokemons = new Dictionary<Pokemon, Vector2Int>(chessBoard.owner.placedPokemons);
        this.challenger = challenger;
        liveChallengerPokemons = new Dictionary<Pokemon, Vector2Int>();
        pokemonPreviousMove = new Dictionary<Pokemon, MoveDirection>();
        ReadyPokemons(chessBoard.owner);
        ReadyPokemons(challenger);

        SetAttackTarget();
    }

    private void ReadyPokemons(Trainer trainer)
    {
        winner = null;
        //Debug.Log(trainer.placedPokemons);
        if(trainer.placedPokemons == null)
        {

        }
        foreach (KeyValuePair<Pokemon, Vector2Int> pokemonAndIndex in trainer.placedPokemons)
        {
            Pokemon pokemon = pokemonAndIndex.Key;
            pokemon.currentState = PokemonState.Move;
            pokemon.battleCallbackHandler = callbackHandler;
            pokemon.isAlive = true;

            Vector2Int index = pokemonAndIndex.Value;
            if (trainer == challenger)
            {
                index = new Vector2Int(7, 7) - index;
                liveChallengerPokemons[pokemon] = index;
            }

            pokemon.transform.position = chessBoard.IndexToWorldPosition(index);
            pokemonsInBattle[index.x, index.y] = pokemon;
            pokemonPreviousMove[pokemon] = MoveDirection.None;
        }
    }

    public void StartBattle()
    {
        battleCoroutine = BattleCoroutine();
        StartCoroutine(battleCoroutine);
    }

    public void EndBattle()
    {
        StopCoroutine(battleCoroutine);
        foreach (KeyValuePair<Pokemon, Vector2Int> pokemonAndIndex in chessBoard.owner.placedPokemons)
        {
            Pokemon pokemon = pokemonAndIndex.Key;
            pokemon.transform.position = chessBoard.IndexToWorldPosition(pokemonAndIndex.Value);
            pokemon.currentHp = pokemon.actualHp;
            pokemon.currentPp = pokemon.initialPp;
            pokemon.isAlive = true;
            pokemon.gameObject.SetActive(true);
        }
        /*foreach (KeyValuePair<Pokemon, Vector2Int> pokemonAndIndex in challenger.placedPokemons)
        {
            Pokemon pokemon = pokemonAndIndex.Key;
            pokemon.transform.position = chessBoard.IndexToWorldPosition(pokemonAndIndex.Value);
            pokemon.currentHp = pokemon.actualHp;
            pokemon.currentPp = pokemon.initialPp;
            pokemon.isAlive = true;
            pokemon.gameObject.SetActive(true);
        }*/
    }

    private IEnumerator BattleCoroutine()
    {
        for (int frame = 0; frame < 60; frame++)
        {
            yield return null;
        }

        MovePokemons(chessBoard.owner);
        MovePokemons(challenger);
        battleCoroutine = BattleCoroutine();
        StartCoroutine(battleCoroutine);
    }
    private void SetAttackTarget()
    {
        SetPokemonsAttackTarget(liveOwnerPokemons, liveChallengerPokemons);
        SetPokemonsAttackTarget(liveChallengerPokemons, liveOwnerPokemons);
    }

    private void MovePokemons(Trainer trainer)
    {
        var attackPokemonsAndIndexes = trainer == chessBoard.owner ?
            liveOwnerPokemons.OrderBy(pokemonAndIndex => pokemonAndIndex.Value.magnitude) :
            liveChallengerPokemons.OrderBy(pokemonAndIndex => -pokemonAndIndex.Value.magnitude);

        var targetPokemonsAndIndex = trainer == chessBoard.owner ?
            liveChallengerPokemons :
            liveOwnerPokemons;


        foreach (KeyValuePair<Pokemon, Vector2Int> attackPokemonAndIndex in attackPokemonsAndIndexes)
        {
            Pokemon attackPokemon = attackPokemonAndIndex.Key;
            if (attackPokemon.currentState == PokemonState.Attack || !attackPokemon.attackTarget.isAlive)
            {
                pokemonPreviousMove[attackPokemon] = MoveDirection.None;
                continue;
            }

            Vector2Int index = attackPokemonAndIndex.Value;

            Vector2Int targetIndex = targetPokemonsAndIndex[attackPokemon.attackTarget];

            if (attackPokemon.transform.position.x > attackPokemon.attackTarget.transform.position.x)
            {
                attackPokemon.spriteRenderer.flipX = false;
            } else
            {
                attackPokemon.spriteRenderer.flipX = true;
            }

            Vector2Int distance = targetIndex - index;

            MoveDirection moveDirection;
            switch (pokemonPreviousMove[attackPokemon])
            {
                case MoveDirection.Up:
                    moveDirection = CalculateMoveDirection(index, distance, canGoDown: false);
                    break;
                case MoveDirection.Down:
                    moveDirection = CalculateMoveDirection(index, distance, canGoUp: false);
                    break;
                case MoveDirection.Left:
                    moveDirection = CalculateMoveDirection(index, distance, canGoRight: false);
                    break;
                case MoveDirection.Right:
                    moveDirection = CalculateMoveDirection(index, distance, canGoLeft: false);
                    break;
                default:
                    moveDirection = CalculateMoveDirection(index, distance);
                    break;
            }

            pokemonsInBattle[index.x, index.y] = null;

            Vector2Int moveTo = index;
            switch (moveDirection)
            {
                case MoveDirection.Up:
                    moveTo += new Vector2Int(0, 1);
                    break;
                case MoveDirection.Down:
                    moveTo += new Vector2Int(0, -1);
                    break;
                case MoveDirection.Right:
                    moveTo += new Vector2Int(1, 0);
                    break;
                case MoveDirection.Left:
                    moveTo += new Vector2Int(-1, 0);
                    break;
                default:
                    break;
            }

            pokemonPreviousMove[attackPokemon] = moveDirection;
            if (trainer == chessBoard.owner)
            {
                liveOwnerPokemons[attackPokemon] = moveTo;
            } else
            {
                liveChallengerPokemons[attackPokemon] = moveTo;
            }
            pokemonsInBattle[moveTo.x, moveTo.y] = attackPokemon;
            attackPokemon.MoveTo(chessBoard.IndexToWorldPosition(moveTo));
        }
    }

    private MoveDirection CalculateMoveDirection(Vector2Int index, Vector2Int distance, bool canGoUp = true, bool canGoDown = true, bool canGoRight = true, bool canGoLeft = true)
    {
        MoveDirection moveDirection = MoveDirection.None;
        Vector2Int moveTo = index;

        Vector2Int absDistance = new Vector2Int(Mathf.Abs(distance.x), Mathf.Abs(distance.y));

        if (absDistance.x >= absDistance.y && (canGoRight || canGoLeft))
        {
            if (distance.x > 0 && canGoRight)
            {
                moveTo = index + new Vector2Int(1, 0);
                moveDirection = MoveDirection.Right;
                if (moveTo.x > 7 || IsAnotherPokemonAlreadyExist(moveTo))
                    return CalculateMoveDirection(index, new Vector2Int(0, distance.y), canGoUp, canGoDown, false, canGoLeft);
            } else if (canGoLeft)
            {
                moveTo = index + new Vector2Int(-1, 0);
                moveDirection = MoveDirection.Left;
                if (moveTo.x < 0 || IsAnotherPokemonAlreadyExist(moveTo))
                    return CalculateMoveDirection(index, new Vector2Int(0, distance.y), canGoUp, canGoDown, canGoRight, false);
            }
        } else if (canGoUp || canGoDown)
        {
            if (distance.y > 0 && canGoUp)
            {
                moveTo = index + new Vector2Int(0, 1);
                moveDirection = MoveDirection.Up;
                if (moveTo.y > 7 || IsAnotherPokemonAlreadyExist(moveTo))
                    return CalculateMoveDirection(index, new Vector2Int(distance.x, 0), false, canGoDown, canGoRight, canGoLeft);
            } else if (canGoDown)
            {
                moveTo = index + new Vector2Int(0, -1);
                moveDirection = MoveDirection.Down;
                if (moveTo.y < 0 || IsAnotherPokemonAlreadyExist(moveTo))
                    return CalculateMoveDirection(index, new Vector2Int(distance.x, 0), canGoUp, false, canGoRight, canGoLeft);
            }
        }

        return moveDirection;
    }

    private bool IsAnotherPokemonAlreadyExist(Vector2Int index)
    {
        return pokemonsInBattle[index.x, index.y] != null;
    }

    private void SetPokemonsAttackTarget(Dictionary<Pokemon, Vector2Int> attackPokemons, Dictionary<Pokemon, Vector2Int> targetPokemons)
    {
        foreach (KeyValuePair<Pokemon, Vector2Int> attackPokemonAndIndex in attackPokemons)
        {
            SetAttackTargetTo(attackPokemonAndIndex, targetPokemons);
        }
    }

    public void SetAttackTargetTo(KeyValuePair<Pokemon, Vector2Int> attackPokemonAndIndex, Dictionary<Pokemon, Vector2Int> targetPokemons)
    {
        Pokemon attackPokemon = attackPokemonAndIndex.Key;
        if (attackPokemon.currentState == PokemonState.Attack) return;

        Vector2Int index = attackPokemonAndIndex.Value;

        List<Pokemon> targetPokemonList = new List<Pokemon>(targetPokemons.Keys);
        List<Vector2Int> targetIndexList = new List<Vector2Int>(targetPokemons.Values);

        Pokemon attackTarget = targetPokemonList[0];
        float minDistance = Vector2Int.Distance(index, targetIndexList[0]);

        for (int i = 1; i < targetPokemons.Count; i++)
        {
            float distance = Vector2Int.Distance(index, targetIndexList[i]);

            if (distance < minDistance)
            {
                minDistance = distance;
                attackTarget = targetPokemonList[i];
            }
        }

        attackPokemon.attackTarget = attackTarget;
    }
    public void PokemonDead(Pokemon pokemon)
    {
        Vector2Int index;
        if (liveChallengerPokemons.ContainsKey(pokemon))
        {
            index = liveChallengerPokemons[pokemon];

            liveChallengerPokemons.Remove(pokemon);

            if (liveChallengerPokemons.Count == 0)
            {
                int temp_damage = 2;
                foreach(Pokemon livepokemon in liveOwnerPokemons.Keys)
                {
                    temp_damage += livepokemon.cost;
                    if (livepokemon.evolutionPhase == 2)
                    {
                        temp_damage += 1;
                    }
                    else if(livepokemon.evolutionPhase == 3)
                    {
                        temp_damage += 3;
                    }
                }
                Victory(chessBoard.owner);
                Trainer_Hp_down(challenger, temp_damage);
            }
        } else
        {
            index = liveOwnerPokemons[pokemon];
            
            liveOwnerPokemons.Remove(pokemon);
            if (liveOwnerPokemons.Count == 0)
            {
                int temp_damage = 2;
                foreach (Pokemon livepokemon in liveChallengerPokemons.Keys)
                {
                    temp_damage += livepokemon.cost;
                    if (livepokemon.evolutionPhase == 2)
                    {
                        temp_damage += 1;
                    }
                    else if (livepokemon.evolutionPhase == 3)
                    {
                        temp_damage += 3;
                    }
                }
                Victory(challenger);
                Trainer_Hp_down(chessBoard.owner, temp_damage);
            }
        }
        pokemonsInBattle[index.x, index.y] = null;
    }

    private void Victory(Trainer trainer)
    {
        winner = trainer;
        Trainer loser = trainer == chessBoard.owner ? challenger : chessBoard.owner;
        foreach (Pokemon pokemon in trainer.placedPokemons.Keys)
        {
            pokemon.currentState = PokemonState.Idle;
        }
        Reset_ChessBoard();
        battleManager.FinishBattleIn(chessBoard, winner, loser);
    }

    public void SetAttackTargetTo(Pokemon attackPokemon)
    {
        if (liveOwnerPokemons.ContainsKey(attackPokemon))
        {
            KeyValuePair<Pokemon, Vector2Int> attackPokemonAndIndex = new KeyValuePair<Pokemon, Vector2Int>(attackPokemon, liveOwnerPokemons[attackPokemon]);
            SetAttackTargetTo(attackPokemonAndIndex, liveChallengerPokemons);
        } else
        {
            KeyValuePair<Pokemon, Vector2Int> attackPokemonAndIndex = new KeyValuePair<Pokemon, Vector2Int>(attackPokemon, liveChallengerPokemons[attackPokemon]);
            SetAttackTargetTo(attackPokemonAndIndex, liveOwnerPokemons);
        }
    }

    public Pokemon GetPokemonInBattle(Vector2Int index)
    {
        return pokemonsInBattle[index.x, index.y];
    }

    public bool IsAttackTargetInRange(Pokemon pokemon)
    {
        Vector2Int distance;
        if (pokemon.trainer == chessBoard.owner)
        {
             distance = liveOwnerPokemons[pokemon] - liveChallengerPokemons[pokemon.attackTarget];
        } else
        {
            distance = liveChallengerPokemons[pokemon] - liveOwnerPokemons[pokemon.attackTarget];
        }

        return Mathf.Abs(distance.x) <= pokemon.range && Mathf.Abs(distance.y) <= pokemon.range;
    }

    public void Execute()
    {
        throw new System.NotImplementedException();
    }

    private void Trainer_Hp_down(Trainer trainer, int damage)
    {
        if(trainer is Stage)
        {
            Debug.Log("스테이지 트레이너이기에 피가 안깍인다");
        }
        else
        {
            trainer.currentHp -= damage;
        }
    }
    private void Reset_ChessBoard()
    {
        List<Pokemon> challengerPokemons = new List<Pokemon>(liveChallengerPokemons.Keys);
        if (challenger is Stage)
        {
            if(liveChallengerPokemons.Count == 0)
            {
                Debug.Log("테스트");
                //영기야 이거 그냥 여기서는 웅이 에니메이터만 없애주면 될것 같아.
                //challenger.GetComponent<Animator>().ResetTrigger("DisAppear");
                //Destroy(challenger.gameObject);
            }
            else
            {
                /*foreach (Pokemon pokemon in challengerPokemons)
                {
                    PokemonUIManager.RemovePokemonUI(pokemon);
                    liveChallengerPokemons.Remove(pokemon);
                }
                Destroy(challenger.gameObject);*/
            }
        }
    }
}