using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletPatternController : MonoBehaviour
{
    public BulletPattern[] bulletPatterns;

    public void FirePattern(int patternIndex)
    {
        if (bulletPatterns[patternIndex] != null)
        {
            bulletPatterns[patternIndex].Fire(transform);
        }
    }
}
