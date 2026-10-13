from server.ratelimit import RateLimiter


def test_a_person_gets_their_allowance_per_window_and_others_are_not_affected():
    now = [0.0]
    limiter = RateLimiter(3, 100.0, clock=lambda: now[0])
    assert [limiter.allow("a") for _ in range(4)] == [True, True, True, False]
    assert limiter.allow("b") is True
    now[0] = 101.0
    assert limiter.allow("a") is True   # the window has passed
