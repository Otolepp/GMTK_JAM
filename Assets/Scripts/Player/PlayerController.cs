using MoreMountains.Feedbacks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    float maxVelocity = 5f;
    const float DRAG_INCREMENT = 5f;
    const float MAX_DRAG = 2.5f;


    Vector2 mouseOnScreen;
    Vector2 mouseDirection;
    Quaternion lookRotation;
    Quaternion adjustRotation;


    Rigidbody2D rb;
    bool isFiring;
    bool isThrust;


    [SerializeField] Camera mainCam;
    [SerializeField] ParticleSystem thrustParticles;

    [Header("Feedbacks")]
    public MMFeedbacks HurtFeedback;
    public MMFeedbacks ShootFeedback;

    ParticleSystem.EmissionModule emission;

    [Header("Stats")]
    [SerializeField] public float maxHealth = 10;
    [SerializeField] HealthBar healthBar;
    [SerializeField] float rotationSpeed = 1;
    [SerializeField] float thrustPower = 300;
    [SerializeField] GameObject shieldGFX;

    public float bulletDamage = 1;
    public float bulletSpeed = 250;
    float lastFired;
    public float health;
    int shieldHealth;

    [Header("Fire")]
    [SerializeField] Transform bulletPos;
    [SerializeField] GameObject bullet1;
    [SerializeField] float fireCd = 0.05f;
    [SerializeField] float randomiseFactor = 6;
    float maxCharge = 10;
    [SerializeField] float fireCharge;
    [SerializeField] float chargeFactor = 2;
    [SerializeField] HealthBar fireChargeBar;
    [SerializeField] GameObject overHeatingGFX;
    bool hasToCoolDown = false;

    [Header("Invincibility")]
    [SerializeField] float invincibilityTime = 1.5f;
    [SerializeField] float invincibilityDeltaTime = 0.15f;
    bool isInvincible = false;
    bool shieldIsInvincible = false;

    public bool IsDead { get { return health <= 0; } }
    public bool HasShield { get { return shieldHealth != 0; } }


    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        emission = thrustParticles.emission;
        emission.enabled = false;

        health = maxHealth;
        healthBar.SetMaxValue(maxHealth);
        healthBar.SetValue(health);

        fireCharge = 0;
        fireChargeBar.SetMaxValue(maxCharge);
        fireChargeBar.SetValue(fireCharge);

    }

    // Update is called once per frame
    void Update()
    {
        mouseOnScreen = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseDirection = (mouseOnScreen - (Vector2)transform.position).normalized;

        isThrust = Input.GetKey(KeyCode.Space);
        isFiring = Input.GetKey(KeyCode.Mouse0);

        if (isFiring && Time.time > lastFired + fireCd && fireCharge < maxCharge && !hasToCoolDown)
        {
            fireCharge += 0.4f;
            if (fireCharge > maxCharge)
            {
                fireCharge = maxCharge;
                hasToCoolDown = true;
                StartCoroutine(ShowOverheatingGFX());
            }

            lastFired = Time.time;
            Fire();
            fireChargeBar.SetValue(fireCharge);
        }

        if (fireCharge > 0)
        {
            fireCharge -= Time.deltaTime * chargeFactor;
            if (fireCharge <= 0)
            {
                fireCharge = 0;
                hasToCoolDown = false;
            }

            if (hasToCoolDown)
            {
                fireChargeBar.SetValueWithoutColor(fireCharge);
            }
            else
            {
                fireChargeBar.SetValue(fireCharge);
            }
        }

        LookAt(mouseOnScreen - (Vector2)transform.position, 180);
    }

    private void FixedUpdate()
    {
        if (isThrust)
        {
            rb.AddForce(mouseDirection * Time.fixedDeltaTime * thrustPower);
            rb.velocity = Vector2.ClampMagnitude(rb.velocity, maxVelocity);
            emission.enabled = true;
        }
        else if (rb.velocity != Vector2.zero)
        {
            emission.enabled = false;
            StartCoroutine(SlowDown());
        }
    }

    void Fire()
    {
        ShootFeedback?.PlayFeedbacks(bulletPos.position);
        mouseOnScreen = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseDirection = (mouseOnScreen - (Vector2)transform.position).normalized;
       
        for(int x =0; x<3; x++)
        {
            Vector2 randomise = Random.insideUnitCircle.normalized;
            Vector2 launchDirection = mouseDirection + (randomise / randomiseFactor);
            Bullet bullet = Instantiate(bullet1, bulletPos.position, Quaternion.identity).GetComponent<Bullet>();
            bullet.Launch(rb.velocity, launchDirection);
        }
    }


    IEnumerator SlowDown()
    {
        while (!isThrust)
        {
            if(rb.drag < MAX_DRAG)
            {
                rb.drag += DRAG_INCREMENT * Time.deltaTime;
            }
            yield return new WaitForEndOfFrame();
        }
        rb.drag = 0;
        yield return new WaitForEndOfFrame();
    }

    void LookAt(Vector3 direction, float offSet=0)
    {
        this.rb.angularVelocity = 0;
        adjustRotation = Quaternion.Euler(0, 0, offSet);
        lookRotation = Quaternion.LookRotation(Vector3.forward, direction) * adjustRotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
    }

    public void Heal()
    {
        Debug.Log("Heal");
        health = Mathf.Clamp(health +1, 0, maxHealth);
        healthBar.SetValue(health);
    }

    public void UpgradeDamage()
    {
        bulletSpeed += 50;
        maxCharge += 2;
        chargeFactor += 0.5f;
    }

    public void UpgradeSpeed()
    {
        thrustPower += 75;
        maxVelocity += 1.5f;
    }

    //TODO
    public void BuyShield()
    {
        shieldHealth = 3;
        shieldGFX.SetActive(true);
    }


    public void TakeDamage(float damage)
    {
        if (isInvincible || shieldIsInvincible)
        {
            return;
        }


        if (HasShield)
        {
            HurtFeedback?.PlayFeedbacks(transform.position);
            shieldHealth--;
            StartCoroutine(ShieldInvincible());
            return;
        }


        health -= damage;
        if (IsDead)
        {
            GameManager.Instance.GameOver();
        }
        else
        {
            HurtFeedback?.PlayFeedbacks(transform.position);
            healthBar.SetValue(health);
            StartCoroutine(BecomeTemporarilyInvincible());
        }
    }

    private IEnumerator ShowOverheatingGFX()
    {
        overHeatingGFX.SetActive(true);
        yield return new WaitForSeconds(.6f);
        while (hasToCoolDown)
        {
            // Alternate between 0 and 1 scale to simulate flashing
            if (overHeatingGFX.transform.localScale == Vector3.one)
            {
                overHeatingGFX.transform.localScale = Vector3.zero;
            }
            else
            {
                overHeatingGFX.transform.localScale = Vector3.one;
            }
            yield return new WaitForSeconds(.6f);
        }

        overHeatingGFX.transform.localScale = Vector3.one;
        overHeatingGFX.SetActive(false);

    }

    private IEnumerator BecomeTemporarilyInvincible()
    {
        isInvincible = true;

        for (float i = 0; i < invincibilityTime; i += invincibilityDeltaTime)
        {
            // Alternate between 0 and 1 scale to simulate flashing
            if (transform.localScale == Vector3.one)
            {
                transform.localScale = Vector3.zero;
            }
            else
            {
                transform.localScale = Vector3.one;
            }
            yield return new WaitForSeconds(invincibilityDeltaTime);
        }

        transform.localScale = Vector3.one;
        isInvincible = false;
    }


    private IEnumerator ShieldInvincible()
    {
        shieldIsInvincible = true;
        for (float i = 0; i < invincibilityTime; i += invincibilityDeltaTime)
        {
            // Alternate between 0 and 1 scale to simulate flashing
            if (shieldGFX.transform.localScale == Vector3.one)
            {
                shieldGFX.transform.localScale = Vector3.zero;
            }
            else
            {
                shieldGFX.transform.localScale = Vector3.one;
            }
            yield return new WaitForSeconds(invincibilityDeltaTime);
        }

        shieldGFX.transform.localScale = Vector3.one;
        shieldIsInvincible = false;
        if (!HasShield)
        {
            shieldGFX.SetActive(false);
        }
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if ((collision.gameObject.CompareTag("Ship") &&  !collision.isTrigger) || collision.gameObject.CompareTag("RopeSegment"))
        {
            TakeDamage(1f);
        }
    }

}
