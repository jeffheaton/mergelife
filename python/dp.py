#!/usr/bin/env python
# From: https://gist.githubusercontent.com/zed/776423/raw/f47971615959f526f2588cb63cb64659148ee3a6/dp.py
"""Find height, width of the largest rectangle containing all 0's in the matrix.

The algorithm for `max_size()` is suggested by @j_random_hacker [1].
The algorithm for `max_rectangle_size()` is from [2].
The Python implementation [3] is dual licensed under CC BY-SA 3.0
and ISC license.

[1]: http://stackoverflow.com/questions/2478447/find-largest-rectangle-containing-only-zeros-in-an-nn-binary-matrix#comment5169734_4671342

[2]: http://blog.csdn.net/arbuckle/archive/2006/05/06/710988.aspx

[3]: http://stackoverflow.com/a/4671342

Copyright (c) 2014, zed <isidore.john.r@gmail.com>

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
"""
from collections import namedtuple
from operator import mul

try:
    reduce = reduce
except NameError:
    from functools import reduce # py3k

Info = namedtuple('Info', 'start height')

def max_size(mat, value=0):
    """Find height, width of the largest rectangle containing all `value`'s.

    For each row solve "Largest Rectangle in a Histrogram" problem [1]:

    [1]: http://blog.csdn.net/arbuckle/archive/2006/05/06/710988.aspx
    """
    it = iter(mat)
    hist = [(el==value) for el in next(it, [])]
    max_size = max_rectangle_size(hist)
    for row in it:
        hist = [(1+h) if el == value else 0 for h, el in zip(hist, row)]
        max_size = max(max_size, max_rectangle_size(hist), key=area)
    return max_size

def max_rectangle_size(histogram):
    """Find height, width of the largest rectangle that fits entirely under
    the histogram.

    >>> f = max_rectangle_size
    >>> f([5,3,1])
    (3, 2)
    >>> f([1,3,5])
    (3, 2)
    >>> f([3,1,5])
    (5, 1)
    >>> f([4,8,3,2,0])
    (3, 3)
    >>> f([4,8,3,1,1,0])
    (3, 3)
    >>> f([1,2,1])
    (1, 3)

    Algorithm is "Linear search using a stack of incomplete subproblems" [1].

    [1]: http://blog.csdn.net/arbuckle/archive/2006/05/06/710988.aspx
    """
    stack = []
    top = lambda: stack[-1]
    max_size = (0, 0) # height, width of the largest rectangle
    pos = 0 # current position in the histogram
    for pos, height in enumerate(histogram):
        start = pos # position where rectangle starts
        while True:
            if not stack or height > top().height:
                stack.append(Info(start, height)) # push
            elif stack and height < top().height:
                max_size = max(max_size, (top().height, (pos - top().start)),
                               key=area)
                start, _ = stack.pop()
                continue
            break # height == top().height goes here

    pos += 1
    for start, height in stack:
        max_size = max(max_size, (height, (pos - start)), key=area)

    return max_size

def area(size):
    return reduce(mul, size)

import unittest
class TestCase(unittest.TestCase):
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

if __name__=="__main__":
    import unittest; unittest.main()
