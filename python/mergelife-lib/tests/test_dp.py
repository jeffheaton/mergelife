"""Largest-rectangle tests, extracted from the embedded unittest block that
previously lived at the bottom of dp.py."""

import unittest

from mergelife.dp import max_size


class TestLargestRect(unittest.TestCase):
    def test(self):
        self.assertEqual(max_size(self.__s2m("""
        0 0 0 0 1 0
        0 0 1 0 0 1
        0 0 0 0 0 0
        1 0 0 0 0 0
        0 0 0 0 0 1
        0 0 1 0 0 0""")), (3, 4))
        self.assertEqual(max_size([[1, 1], [0, 0]]), (1, 2))
        self.assertEqual(max_size([[0, 0], [1, 1]]), (1, 2))
        self.assertEqual(max_size([[1, 0], [1, 0]]), (2, 1))
        self.assertEqual(max_size([[0, 1], [0, 1]]), (2, 1))
        self.assertEqual(max_size(self.__s2m("""
        0 0 0 0 1 0
        0 0 1 0 0 1
        0 0 0 0 0 0
        1 0 0 0 0 0
        0 0 0 0 0 1
        0 0 1 0 0 0
        0 0 0 0 0 0
        0 0 0 0 0 0""")), (7, 2))
        self.assertEqual(max_size([[]]), (0, 0))
        self.assertEqual(max_size([]), (0, 0))
        self.assertEqual(max_size(self.__s2m("""
        0 0 0 0 1 0
        0 0 1 0 0 1
        0 0 0 0 0 0
        1 0 0 0 0 0
        0 0 0 0 0 0
        0 0 1 0 0 1
        0 0 0 0 0 0
        0 0 0 0 0 0""")), (3, 5))
        self.assertEqual(max_size(self.__s2m("""
        0 0 0 0 1 0
        0 0 0 0 0 0
        0 0 1 0 0 1
        0 0 0 0 0 0
        1 0 0 0 0 0
        0 0 0 0 0 0
        0 0 1 0 0 1
        0 0 0 0 0 0
        0 0 0 0 0 1""")), (8, 2))
        self.assertEqual(max_size(self.__s2m("""
        0 0 0 0 1 1 1
        0 0 0 0 0 0 0
        0 0 0 1 1 1 1
        0 0 1 1 1 1 1
        1 0 1 1 1 1 1
        1 0 1 1 1 1 1
        1 0 1 1 1 1 1
        """)), (3, 3))

    def __s2m(self, s):
        return [map(int, line.split())
                for line in s.splitlines() if line.strip()]
