def get_bool(settings, key):
    result = settings.get(key, False)
    if not result:
        result = False
    return result


def get_int(settings, key):
    result = settings.get(key, 1)
    if not result:
        result = 1
    return result


def set_combo(cb, value):
    idx = cb.findText(value)
    if idx >= 0:
        cb.setCurrentIndex(idx)
    else:
        cb.setCurrentIndex(0)
