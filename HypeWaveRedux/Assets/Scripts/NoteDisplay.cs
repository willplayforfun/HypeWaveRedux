using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoteDisplay : MonoBehaviour {

    [SerializeField]
    private Sprite[] fretboardSprites;

    internal void SetFretboardSprite(int playerNum)
    {
        if (playerNum >= 0 && playerNum < fretboardSprites.Length)
        {
            fretboardSprite.sprite = fretboardSprites[playerNum];
        }
    }

    [SerializeField]
    private SpriteRenderer fretboardSprite;

    [SerializeField]
    private SpriteRenderer[] feedbackSprites;

    [SerializeField]
    private Transform skillBarContainer;

    [SerializeField]
    private SpriteRenderer skillBarSprite;
    [SerializeField]
    private Color badSkillsColor;

    [SerializeField]
    private TextMesh comboTracker;

    // the world position we are currently tracking to
    internal Vector3 targetPosition;

    // our player
    internal Player player;

    // how many correct notes in a row the player has gotten
    private int combo;

    private void Update()
    {
        // update combo number
        comboTracker.text = "x" + combo;

        // update skill bar
        float skill = player.GetSkill();
        if (skill < 0)
        {
            skillBarSprite.color = badSkillsColor;
            skillBarContainer.localScale = new Vector3(Mathf.InverseLerp(0, Mathf.Abs(player.minSkills), Mathf.Abs(skill)), 1, 1);
        }
        else
        {
            skillBarSprite.color = Color.white;
            skillBarContainer.localScale = new Vector3(Mathf.InverseLerp(0, player.maxSkills, skill), 1, 1);
        }

        // go to tracking position
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime / 0.3f);
    }

    /// <summary>
    /// Makes the display disappear
    /// </summary>
    internal void PlayerDied()
    {
        Hide();
    }

    /// <summary>
    /// Makes the display re-appear
    /// </summary>
    internal void PlayerRespawned()
    {
        Show();
    }

    /// <summary>
    /// Sets opacity to max
    /// </summary>
    internal void NoPlayersOccluding()
    {
        fretboardSprite.color = new Color(1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// Makes the display transparent so you can see players behind it
    /// </summary>
    internal void PlayerIsOccluding()
    {
        fretboardSprite.color = new Color(1f, 1f, 1f, 0.8f);
    }

    private void Hide()
    {
        fretboardSprite.enabled = false;
        foreach(SpriteRenderer sprite in feedbackSprites)
        {
            sprite.enabled = false;
        }
        skillBarSprite.enabled = false;
        comboTracker.gameObject.SetActive(false);
    }

    private void Show()
    {
        fretboardSprite.enabled = true;
        foreach (SpriteRenderer sprite in feedbackSprites)
        {
            sprite.enabled = true;
        }
        skillBarSprite.enabled = true;
        comboTracker.gameObject.SetActive(true);
    }
}
