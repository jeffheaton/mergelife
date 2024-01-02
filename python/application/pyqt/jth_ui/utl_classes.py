import importlib


def get_class_full_name(instance):
    """Returns the full name of the class of the given instance."""
    return instance.__class__.__module__ + "." + instance.__class__.__name__


def create_instance_from_full_name(full_name):
    """Creates an instance of the class with the given full name."""
    module_name, class_name = full_name.rsplit(".", 1)
    module = importlib.import_module(module_name)
    cls = getattr(module, class_name)
    return cls()
