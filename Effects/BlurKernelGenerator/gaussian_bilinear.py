import math
import sys

from numpy import float32


def gaussian_kernel(half_radius: int) -> tuple[float, list[float], list[float]]:
    radius = half_radius * 2
    
    coefficients = [1]
    for _ in range(2 * radius):
        tmp = [1]
        for i in range(1, len(coefficients)):
            tmp.append(coefficients[i - 1] + coefficients[i])
        tmp.append(1)
        coefficients = tmp

    coefficient_sum = sum(coefficients)
    coefficients = [coefficients[i] / coefficient_sum for i in range(radius, -1, -1)]
    
    bilinear_coefficients = []
    offsets = []
    for i in range(1, radius, 2):
        a = coefficients[i]
        b = coefficients[i + 1]
        c = a + b
        bilinear_coefficients.append(c)
        offsets.append(i + b / c)
        
    return coefficients[0], bilinear_coefficients, offsets


def main() -> None:
    half_radius = int(sys.argv[1])
    center_weight, weights, offsets = gaussian_kernel(half_radius)

    print(f"Radius = {2 * half_radius + 0.5}")
    print(f"Center Weight = {str(float32(center_weight))}")
    print(f"Weights = {{ {', '.join(str(float32(x)) for x in weights)} }}")
    print(f"Offsets = {{ {', '.join(str(float32(x)) for x in offsets)} }}")


if __name__ == "__main__":
    main()
