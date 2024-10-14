using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthManagerUI : MonoBehaviour
{
    public Health playerHealth;
    public Health enemyHealth;

    public Slider playerHealthUI;
    public Slider enemyHealthUI;

    public Image playerHealthFill;
    public Image enemyHealthFill;

    // Start is called before the first frame update
    void Start()
    {
        playerHealthUI.maxValue = playerHealth.maxHealth;
        playerHealthUI.value = playerHealth.currentHealth;

        enemyHealthUI.maxValue = enemyHealth.maxHealth;
        enemyHealthUI.value = enemyHealth.currentHealth;
    }

    // Update is called once per frame
    void Update()
    {
        playerHealthUI.value = playerHealth.currentHealth;
        enemyHealthUI.value = enemyHealth.currentHealth;

        ToggleHealthFill(playerHealth.currentHealth, playerHealthFill);
        ToggleHealthFill(enemyHealth.currentHealth, enemyHealthFill);
    }

    void ToggleHealthFill(float currentHealth, Image fillImage)
    {
        if (currentHealth <= 0) 
        {
            fillImage.enabled = false;
        }
        else
        {
            fillImage.enabled = true;
        }
    }
}
